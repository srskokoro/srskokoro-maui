namespace Kokoro.Test.Framework.Attributes;
using System;
using System.Text.RegularExpressions;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TLabelAttribute : LabelAttribute {
	public static readonly Regex ConformingTestNamePattern = new(@"^([TD](\d+))(?:_(\w+))?", RegexOptions.Compiled);

	private static readonly Regex MemberInFormat_Or_EscAsciiPunc_Pattern = new(@"\[([mcp._x?]\!?)\]|\\([!-/:-@[-`{-~])", RegexOptions.Compiled);

	public virtual int TestNumber { get; protected set; }

	protected TLabelAttribute() { }

	public TLabelAttribute(string? format = null, [CallerMemberName] string testMethodName = "", string? labelSeparator = null) {
		LabelSeparator = labelSeparator;

		Match match = ConformingTestNamePattern.Match(testMethodName);
		if (!match.Success) {
			TestNumber = -1; // Indicate nonconformance
		} else {
			TestNumber = int.Parse(match.Groups[2].ValueSpan);

			Group targetOfTest = match.Groups[3];
			if (!targetOfTest.Success) {
				goto Done;
			}
			TestMethodNameOverride = match.Groups[1].Value; // "T0", "T1", etc.

			// Now, transform the format string to inline the target of the
			// test.

			// Convention:
			//
			// - '[m]' means a method member or callable function
			// - '[c]' same as '[m]'
			//
			// - '[p]' means a property or field member
			//
			// - '[.]' means an inline code; same appearance as '[p]'
			// - '[_]' same as '[.]'
			// - '[x]' same as '[.]'
			// - '[?]' same as '[.]'
			//
			// - '[_!]' means inline in backticks -- replace the '_' with the
			// character from any of the above formats.

			if (format is null) {
				format = "[.]";
			}

			string? callInBackticks = null;
			string? callNoBackticks = null;

			string? codeInBackticks = null;
			string? codeNoBackticks = null;

			format = MemberInFormat_Or_EscAsciiPunc_Pattern.Replace(format, match => {
				Group escPunc = match.Groups[2];
				if (escPunc.Success) {
					return escPunc.Value;
				}

				Group inlineType = match.Groups[1];
				var inlineTypeSpan = inlineType.ValueSpan;

				bool inlineInBackticks = inlineTypeSpan.Length > 1;
				char inlineTypeChar = inlineTypeSpan[0];

				string? text;
				switch (inlineTypeChar) {
					case 'm':
					case 'c': {
						if (inlineInBackticks) {
							text = callInBackticks;
							if (text is null) {
								text = $"`{targetOfTest}()`";
								callInBackticks = text;
							}
						} else {
							text = callNoBackticks;
							if (text is null) {
								text = $"{targetOfTest}()";
								callNoBackticks = text;
							}
						}
						break;
					}
					case 'p':
					case '.':
					case '_':
					case 'x':
					case '?': {
						if (inlineInBackticks) {
							text = codeInBackticks;
							if (text is null) {
								text = $"`{targetOfTest}`";
								codeInBackticks = text;
							}
						} else {
							text = codeNoBackticks;
							if (text is null) {
								text = targetOfTest.Value;
								codeNoBackticks = text;
							}
						}
						break;
					}
					default: {
						Trace.Fail(
							$"Character not handled at index " +
							$"{inlineType.Index}: '{inlineTypeChar}'"
						);
						text = match.Value;
						break;
					}
				}
				return text;
			});
		}

	Done:
		Text = format;
	}
}

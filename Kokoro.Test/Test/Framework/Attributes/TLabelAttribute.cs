namespace Kokoro.Test.Framework.Attributes;
using System;
using System.Text.RegularExpressions;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TLabelAttribute : LabelAttribute {
	public static readonly Regex ConformingTestNamePattern = new(@"^(T(\d+))(?:_(\w+))?", RegexOptions.Compiled);

	private static readonly Regex MemberInFormat_Or_EscAsciiPunc_Pattern = new(@"\[([mcp._x~?])\]|\\([!-/:-@[-`{-~])", RegexOptions.Compiled);

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
			//
			// - '[x]' means inline without backticks
			// - '[~]' same as '[x]'
			// - '[?]' same as '[x]'

			if (format is null) {
				format = "[.]";
			}

			string? callInBackticks = null;
			string? codeInBackticks = null;
			string? textNoBackticks = null;

			format = MemberInFormat_Or_EscAsciiPunc_Pattern.Replace(format, match => {
				Group escPunc = match.Groups[2];
				if (escPunc.Success) {
					return escPunc.Value;
				}

				Group inlineType = match.Groups[1];
				char inlineTypeChar = inlineType.ValueSpan[0];

				switch (inlineTypeChar) {
					case 'm':
					case 'c': {
						if (callInBackticks is null) {
							callInBackticks = $"`{targetOfTest}()`";
						}
						return callInBackticks;
					}
					case 'p':
					case '.':
					case '_': {
						if (codeInBackticks is null) {
							codeInBackticks = $"`{targetOfTest}`";
						}
						return codeInBackticks;
					}
					case 'x':
					case '~':
					case '?': {
						if (textNoBackticks is null) {
							textNoBackticks = targetOfTest.Value;
						}
						return textNoBackticks;
					}
					default: {
						Trace.Fail(
							$"Character not handled at index " +
							$"{inlineType.Index}: '{inlineTypeChar}'"
						);
						return match.Value;
					}
				}
			});
		}

	Done:
		Text = format;
	}
}

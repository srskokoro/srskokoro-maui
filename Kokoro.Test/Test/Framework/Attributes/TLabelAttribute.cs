namespace Kokoro.Test.Framework.Attributes;

using System;
using System.Text.RegularExpressions;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal class TLabelAttribute : LabelAttribute {
	public static readonly Regex ConformingTestNamePattern = new(@"^(T\d+)(?:_([a-zA-Z0-9])_(\w+)$)?", RegexOptions.Compiled);

	private static readonly Regex MemberInFormat_Or_EscAsciiPunc_Pattern = new(@"\[[ !$%&*.?^_~]\]|\\([!-/:-@[-`{-~])", RegexOptions.Compiled);

	protected TLabelAttribute() { }

	public TLabelAttribute(string? format, [CallerMemberName] string testMethodName = "", string? labelSeparator = null) {
		LabelSeparator = labelSeparator;

		Match match = ConformingTestNamePattern.Match(testMethodName);
		if (match.Success) {
			var targetOfTest = match.Groups[3].ValueSpan;
			if (targetOfTest.IsEmpty) {
				goto Done;
			}

			TestMethodNameOverride = match.Groups[1].Value; // "T0", "T1", etc.

			if (format is null) {
				goto Done;
			}

			var memberType = match.Groups[2].ValueSpan; // 'm', 'p', 'x', etc.

			// Convention:
			// - 'm' means method member
			// - 'p' means property or field member
			// - 'x' means unknown or explicit opt-out
			var targetText = memberType.IsEmpty || memberType[0] != 'm'
				? $"`{targetOfTest}`"
				: $"`{targetOfTest}()`";

			format = MemberInFormat_Or_EscAsciiPunc_Pattern.Replace(format, match => {
				var escPunc = match.Groups[1];
				if (escPunc.Success) {
					return escPunc.Value;
				}
				return targetText;
			});
		}

	Done:
		Text = format;
	}
}

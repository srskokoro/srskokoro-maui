namespace Kokoro.Test.Util;

public static class BoolExtensions {

	/// <summary>
	/// See, <see href="https://stackoverflow.com/questions/491334/why-does-boolean-tostring-output-true-and-not-true"/>
	/// <para>
	/// See also, <see cref="System.Xml.XmlConvert.ToString(bool)"/>
	/// </para>
	/// </summary>
	public static string ToKeyword(this bool @bool) => @bool ? "true" : "false";
}

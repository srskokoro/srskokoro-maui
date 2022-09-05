namespace Kokoro;

public sealed partial class FieldVal {

	public static FieldVal From(string text) => new(FieldTypeHint.Text, text.ToUTF8Bytes());

	public static FieldVal From(byte[] blob) => new(FieldTypeHint.Blob, blob);
}

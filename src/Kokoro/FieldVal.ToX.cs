namespace Kokoro;

public sealed partial class FieldVal {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string? GetText() {
		if (_TypeHint == FieldTypeHint.Text) {
			return TextUtils.UTF8ToString(_Data);
		}
		return null;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ReadOnlySpan<byte> GetBlob() {
		if (_TypeHint == FieldTypeHint.Blob) {
			return _Data;
		}
		return default;
	}
}

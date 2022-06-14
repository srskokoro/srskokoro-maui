namespace Kokoro.Common.Blake2Fast.Extensions;
using global::Blake2Fast.Implementation;
using Kokoro.Common.Util;

internal static partial class Blake2bHashStateExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static Span<byte> FinishAndGet(ref this Blake2bHashState state, Span<byte> output) {
		state.Finish(output);
		return output;
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static void UpdateWithVarInt(ref this Blake2bHashState state, uint input) {
		Span<byte> buffer = stackalloc byte[5];
		int written = VarInts.Write(buffer, input);
		state.Update(buffer[..written]);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SkipLocalsInit]
	public static void UpdateWithVarInt(ref this Blake2bHashState state, ulong input) {
		Span<byte> buffer = stackalloc byte[9];
		int written = VarInts.Write(buffer, input);
		state.Update(buffer[..written]);
	}
}

namespace Kokoro.Common.Blake2Fast.Extensions;
using global::Blake2Fast.Implementation;
using Kokoro.Common.Util;
using System.Buffers.Binary;

internal static partial class Blake2bHashStateExtensions {

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

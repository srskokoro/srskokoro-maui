namespace Kokoro.Common;
using System.Buffers;
using System.Runtime.InteropServices;

internal static class Strings {

	/// <summary>
	/// Equivalent to <see cref="string.Create{TState}">string.Create&lt;TState&gt;(…)</see>
	/// without needing to allocate a <see cref="SpanAction{char, TState}">SpanAction&lt;char, TState&gt;</see>
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref char UnsafeCreate(int length, out string result) {
		// Given how strings are represented in memory, and that we're creating
		// via `new string()`, it's unlikely that the trick below will break
		// something. That is, it should be guaranteed that we'll get a
		// different reference (or `ref char` location) every time.
		result = new('\0', length);
		return ref MemoryMarshal.GetReference<char>(result);
	}
}

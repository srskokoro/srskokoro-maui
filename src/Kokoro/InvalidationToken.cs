﻿namespace Kokoro;
using Kokoro.Internal;

/// <summary>
/// Useful for keeping track of necessary invalidations, by providing an
/// indication that the collection data has been modified.
/// </summary>
public class InvalidationToken {
	internal InvalidationSource Source;
	internal nuint DataMark = InvalidationSource.DataMarkInit;

	public InvalidationToken(KokoroCollection host) => Source = host.InvSrc;

	/// <summary>
	/// Returns <see langword="true"/> if it's very likely that the collection
	/// wasn't modified since <see cref="SetFresh()"/> was last called.
	/// </summary>
	public bool IsQuiteFresh {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		get => DataMark != Source.DataMark ? false : true;
	}

	/// <summary>
	/// Returns <see langword="true"/> if the collection really wasn't modified
	/// since <see cref="SetFresh()"/> was last called.
	/// </summary>
	public bool IsReallyFresh {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			if (IsQuiteFresh) {
				if (!Source.OwnerDb.UpdateInvSrc()) {
					return true; // Still really fresh
				}
			}
			return false;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SetFresh() {
		var curmark = Source.DataMark;
		if (curmark != InvalidationSource.DataMarkExhausted) {
			DataMark = curmark;
			return;
		}
		ResolveSource();
	}

	// Not inlined, as this is meant to be called much rarely
	[MethodImpl(MethodImplOptions.NoInlining)]
	private void ResolveSource() {
		// Throws if host is already disposed
		var latest = Source.Latest;
		Source = latest;

		var curmark = latest.DataMark;
		DataMark = curmark;

		Debug.Assert(curmark != InvalidationSource.DataMarkExhausted);
	}
}

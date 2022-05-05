namespace Kokoro.Internal;

public abstract class DataEntity {
	internal DataToken DataToken;
	internal nuint DataMark = DataToken.DataMarkInit;

	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	private protected DataEntity(KokoroCollection host) {
		DataToken = host.DataToken;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private protected void RefreshDataMark() {
		var curmark = DataToken.DataMark;
		if (curmark != DataToken.DataMarkExhausted) {
			DataMark = curmark;
			return;
		}
		ResolveDataToken();
	}

	// Not inlined, as this is meant to be called much rarely
	[MethodImpl(MethodImplOptions.NoInlining)]
	private void ResolveDataToken() {
		// Throws if collection already disposed
		var latest = DataToken.Latest;
		DataToken = latest;

		var curmark = latest.DataMark;
		DataMark = curmark;

		Debug.Assert(curmark != DataToken.DataMarkExhausted);
	}

	/// <summary>
	/// Returns <see langword="true"/> if it's very likely that the collection
	/// wasn't modified since any of this object's state was last loaded.
	/// </summary>
	public bool IsQuiteFresh {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// Ternary operator returning true/false prevents redundant asm generation:
		// See, https://github.com/dotnet/runtime/issues/4207#issuecomment-147184273
		get => DataMark == DataToken.DataMark ? true : false;
	}

	/// <summary>
	/// Loads the object's core state. Any pending changes to the core state
	/// will be discarded. Also, <see cref="Unload()">unloads</see> the current
	/// state (not just the preexisting core state) if it would be inconsistent
	/// with the newly loaded core state.
	/// </summary>
	/// <remarks>
	/// Any preexisting state should at least be considered inconsistent if
	/// <see cref="IsQuiteFresh"/> is now <see langword="false"/>.
	/// </remarks>
	/// <seealso cref="Unload()"/>
	public abstract void Load();

	/// <summary>
	/// Unloads the current state (not just the core state), discarding any
	/// cached data and pending changes.
	/// </summary>
	public abstract void Unload();

	/// <summary>
	/// Unloads the current state (not just the core state), discarding any
	/// cached data and pending changes, then loads the object's core state.
	/// </summary>
	public abstract void Reload();

	/// <summary>
	/// Does nothing while <see cref="IsQuiteFresh"/> is still <see langword="true"/>.
	/// Otherwise, this will unload the current state (not just the core state),
	/// discarding any cached data and pending changes, then loads the object's
	/// core state.
	/// </summary>
	/// <remarks>
	/// The default implementation simply calls <see cref="Load()"/> if <see cref="IsQuiteFresh"/>
	/// is now <see langword="false"/>.
	/// </remarks>
	public virtual void ReloadStale() {
		if (IsQuiteFresh) return;
		Load();
	}
}

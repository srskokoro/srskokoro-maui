namespace Kokoro;

public class KokoroCollection : IDisposable, IAsyncDisposable {
	private readonly KokoroContext _Context;

	public KokoroContext Context => _Context;

	internal const int _OperableVersion = 1;

	public static int OperableVersion => _OperableVersion;

	protected internal KokoroCollection(KokoroContext context) {
		// A check should be done prior to instantiation instead.
		// But, this check is for subclasses' sake.
		CheckIfOperable(context);
		_Context = context;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void CheckIfOperable(KokoroContext context) {
		if (!context.IsOperable)
			throw Ex__NSE_VersionNotOperable();
	}

	#region `IDisposable` implementation

	// Provided only for subclasses' sake.
	protected virtual void Dispose(bool disposing) => _Context.Dispose(disposing);

	public void Dispose() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	public virtual ValueTask DisposeAsync() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
		return default;
	}

	#endregion

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static NotSupportedException Ex__NSE_VersionNotOperable()
		=> new($"Version is not operable. Please migrate the " +
			// TODO Consider using `GetType()` instead? -- for subclassing purposes
			$"`{nameof(KokoroContext)}` first to the current operable vesrion.");
}

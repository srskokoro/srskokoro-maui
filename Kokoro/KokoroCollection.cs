namespace Kokoro;

/// <remarks>Not thread-safe.</remarks>
public class KokoroCollection : IDisposable, IAsyncDisposable {

	public static int OperableVersion => 1;

	protected internal readonly KokoroSqliteDb db;
	private readonly KokoroContext context;

	public KokoroContext Context => context;

	protected internal KokoroCollection(KokoroContext context) {
		// A check should be done prior to instantiation instead.
		// But, this check is for subclasses' sake.
		CheckIfOperable(context);

		this.db = context.db;
		this.context = context;
	}

	internal static void CheckIfOperable(KokoroContext context) {
		if (!context.IsOperable)
			throw new NotSupportedException($"Version is not operable. Please migrate the `{nameof(KokoroContext)}` first to the current operable vesrion.");
	}

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
	public virtual void Dispose() => context.Dispose();
	public virtual ValueTask DisposeAsync() => context.DisposeAsync();
#pragma warning restore CA1816
}

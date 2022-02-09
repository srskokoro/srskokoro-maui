﻿namespace Kokoro;

/// <remarks>Not thread-safe.</remarks>
public class KokoroCollection : IDisposable, IAsyncDisposable {

	public static int OperableVersion => 1;

	protected internal readonly KokoroSqliteDb _db;
	private readonly KokoroContext _context;

	public KokoroContext Context => _context;

	protected internal KokoroCollection(KokoroContext context) {
		// A check should be done prior to instantiation instead.
		// But, this check is for subclasses' sake.
		CheckIfOperable(context);

		_db = context._db;
		_context = context;
	}

	internal static void CheckIfOperable(KokoroContext context) {
		if (!context.IsOperable)
			throw new NotSupportedException($"Version is not operable. Please migrate the `{nameof(KokoroContext)}` first to the current operable vesrion.");
	}

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
	public virtual void Dispose() => _context.Dispose();
	public virtual ValueTask DisposeAsync() => _context.DisposeAsync();
#pragma warning restore CA1816
}

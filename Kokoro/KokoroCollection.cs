using Microsoft.Data.Sqlite;

namespace Kokoro;

/// <remarks>Not thread safe.</remarks>
public class KokoroCollection {

	public static int OperableVersion => 1;

	protected internal readonly SqliteConnection _db;
	private readonly KokoroContext _context;

	public KokoroContext Context => _context;

	protected internal KokoroCollection(KokoroContext context) {
		ThrowIfNotOperable(context);
		_db = context._db;
		_context = context;
	}

	internal static void ThrowIfNotOperable(KokoroContext context) {
		if (!context.IsOperable)
			throw new NotSupportedException($"Version is not operable. Please migrate the `{nameof(KokoroContext)}` first to the current operable vesrion.");
	}
}

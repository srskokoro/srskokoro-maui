namespace Kokoro.Internal;

public abstract class DataEntity {
	private readonly KokoroCollection _Host;

	public KokoroCollection Host => _Host;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public DataEntity(KokoroCollection host) => _Host = host;
}

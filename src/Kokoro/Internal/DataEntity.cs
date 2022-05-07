namespace Kokoro.Internal;

public abstract class DataEntity {
	private readonly KokoroCollection _Host;

	public KokoroCollection Host => _Host;

	public DataEntity(KokoroCollection host) => _Host = host;
}

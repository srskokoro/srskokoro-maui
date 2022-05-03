namespace Kokoro;

public class KokoroCollectionExclusive : KokoroCollection {

	public KokoroCollectionExclusive(KokoroContext context) : base(context) { }

	private protected override void MarkUsage(KokoroContext context) => context.MarkUsageExclusive();
	private protected override void UnMarkUsage(KokoroContext context, bool disposing) => context.UnMarkUsageExclusive(disposing);
}

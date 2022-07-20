namespace Kokoro.Common.IO;

internal static class StreamUtils {
	public const int DefaultCopyBufferSize = 8192;

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static InvalidOperationException Ex_EndOfStreamRead_InvOp()
		=> new("The stream didn't contain enough data to read the requested item.");

	[DoesNotReturn]
	public static void E_EndOfStreamRead_InvOp() => throw Ex_EndOfStreamRead_InvOp();

	[DoesNotReturn]
	public static T E_EndOfStreamRead_InvOp<T>() => throw Ex_EndOfStreamRead_InvOp();
}

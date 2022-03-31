global using static ThisAssembly.GlobalConstants;

partial class ThisAssembly {

	// --

	#region Global Constants

	public static partial class GlobalConstants {
#if DEBUG
		public const bool DEBUG = true;
#else
		public const bool DEBUG = false;
#endif

#if TEST
		public const bool TEST = true;
#else
		public const bool TEST = false;
#endif
	}

	static partial class GlobalConstants__global_using__prevent_mark_as_unused {
		static class X { static X() => _ = DEBUG; }
	}

	#endregion
}

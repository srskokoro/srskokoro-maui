namespace Kokoro.Internal;
using Kokoro.Internal.Sqlite;
using System.Buffers;

partial class FieldedEntity {
	// Ideally, less than the default page size of SQLite version 3.12.0
	// (2016-03-29), which is 4096.
	internal const int DefaultHotStoreLimit = 3328;

	internal const int MaxFieldCount = byte.MaxValue;
	internal const int MaxFieldValsLength = 0xFF_FFFF;

	private protected struct FieldsWriter {

		internal int[] _Offsets;
		internal FieldVal?[] _Entries;

		internal int _HotStoreLength, _ColdStoreLength; // -1 if should skip
		internal FieldsDesc _HotFieldsDesc, _ColdFieldsDesc;

		internal List<(long Id, FieldVal Entry)>? _FloatingFields;

		public readonly int HotStoreLength => _HotStoreLength;
		public readonly int ColdStoreLength => _ColdStoreLength;

		public readonly List<(long Id, FieldVal Entry)>? FloatingFields => _FloatingFields;

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		public readonly void WriteHotStore(Stream destination) {
#if DEBUG
			Debug.Assert(_HotStoreLength > 0, $"Shouldn't be called while data length <= 0 (currently {_HotStoreLength})");
			long expectedEndPos = destination.Position + _HotStoreLength;
#endif
			// TODO Implement
#if DEBUG
			Debug.Assert(destination.Position == expectedEndPos);
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		public readonly void WriteColdStore(Stream destination) {
#if DEBUG
			Debug.Assert(_ColdStoreLength > 0, $"Shouldn't be called while data length <= 0 (currently {_ColdStoreLength})");
			long expectedEndPos = destination.Position + _ColdStoreLength;
#endif
			// TODO Implement
#if DEBUG
			Debug.Assert(destination.Position == expectedEndPos);
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)] // Use fully optimizing JIT, right from the start!
		public void Dispose() {
			// NOTE: Client code should ensure that we're always disposed, even
			// when rewriting fails (i.e., even when an exception occurs) and
			// the buffers were not initialized.
			var entries = _Entries;
			if (entries != null) {
				ArrayPool<FieldVal?>.Shared.ReturnClearingReferences(entries);
				_Entries = null!;
			}
			var offsets = _Offsets;
			if (offsets != null) {
				ArrayPool<int>.Shared.ReturnClearingReferences(offsets);
				_Offsets = null!;
			}
		}
	}

	[Conditional("DEBUG")]
	private static void DAssert_FieldsWriterPriorRewrite(ref FieldsWriter fw) {
		Debug.Assert(fw._Offsets == null,
			$"`{nameof(fw._Offsets)}` must be null prior a fields rewrite");

		Debug.Assert(fw._Entries == null,
			$"`{nameof(fw._Entries)}` must be null prior a fields rewrite");

		Debug.Assert(fw._FloatingFields is null or { Count: 0 },
			$"`{nameof(fw._FloatingFields)}` must be null or empty prior a fields rewrite");
	}

	private protected bool MayCompileFieldChanges {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		get {
			var fields = _Fields;
			if (fields != null && fields.Changes != null)
				return true;
			return false;
		}
	}

	/// <remarks>
	/// <para>
	/// NOTE: This method may modify <see cref="_SchemaId"/> on successful
	/// return. If it's important that the old value of <see cref="_SchemaId"/>
	/// be preserved, perform a manual backup of the old value before the call.
	/// </para>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="NestingWriteTransaction"/>).
	/// <br/>- Must load <see cref="_SchemaId"/> beforehand, at least once,
	/// while inside the transaction.
	/// <br/>- Must load <see cref="_SchemaId"/> with the rowid of the actual
	/// schema being used by the <see cref="FieldedEntity">fielded entity</see>.
	/// <br/>- Must be called while <see cref="MayCompileFieldChanges"/> is <see langword="true"/>.
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private protected void CompileFieldChanges(ref FieldsReader fr, ref FieldsWriter fw, int hotStoreLimit = DefaultHotStoreLimit) {
		DAssert_FieldsWriterPriorRewrite(ref fw);

		Fields? fields = _Fields;
		Debug.Assert(fields != null, $"`{nameof(_Fields)}` must be non-null " +
			$"prior to calling this method (see also, `{nameof(MayCompileFieldChanges)}`)");

		FieldChanges? fchanges = fields.Changes;
		Debug.Assert(fchanges != null, $"`{nameof(_Fields)}.{nameof(_Fields.Changes)}` must be non-null " +
			$"prior to calling this method (see also, `{nameof(MayCompileFieldChanges)}`)");

		var fchanges_iter = fchanges.GetEnumerator();
		if (!fchanges_iter.MoveNext()) goto NoFieldChanges;

		// TODO Implement

	NoFieldChanges:
		fw._ColdStoreLength = fw._HotStoreLength = -1;
		return;
	}

	[DoesNotReturn]
	private void E_TooManyFields(int count) {
		Debug.Assert(count > MaxFieldCount);
		throw new InvalidOperationException(
			$"Total number of fields (currently {count}) shouldn't exceed {MaxFieldCount};" +
			$"{Environment.NewLine}Entity: {GetDebugLabel()};" +
			$"{Environment.NewLine}Schema: {_SchemaId};");
	}

	// --

	[Conditional("DEBUG")]
	private static void DInit_StoreLengthsAndFDescs(ref FieldsWriter fw) {
		fw._ColdStoreLength = fw._HotStoreLength = -2;
		fw._ColdFieldsDesc = fw._HotFieldsDesc = -1;
	}

	[Conditional("DEBUG")]
	private static void DAssert_StoreLengthsAndFDescs(ref FieldsWriter fw) {
		Debug.Assert(fw._HotStoreLength >= -1);
		Debug.Assert(fw._ColdStoreLength >= -1);

		// Necessary for the corrrectness of the code after
		Debug.Assert(FieldsDesc.MaxValue == int.MaxValue);

		Debug.Assert(
			(fw._HotStoreLength <= 0 && fw._ColdStoreLength <= 0) ||
			(uint)fw._HotFieldsDesc <= (uint)FieldsDesc.MaxValue
		);
		Debug.Assert(
			fw._ColdStoreLength <= 0 ||
			(uint)fw._ColdFieldsDesc <= (uint)FieldsDesc.MaxValue
		);
	}
}

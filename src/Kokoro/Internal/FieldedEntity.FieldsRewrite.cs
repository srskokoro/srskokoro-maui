namespace Kokoro.Internal;
using Kokoro.Common.Buffers;
using Kokoro.Common.Util;
using Microsoft.Data.Sqlite;
using System.Buffers;
using System.Runtime.InteropServices;

partial class FieldedEntity {

	private protected struct FieldsWriter {

		internal record struct Entry(
			FieldVal? Override, LatentFieldVal OrigValue
		);

		internal int[] _Offsets;
		internal Entry[] _Entries;

		internal int _HotStoreLength, _ColdStoreLength; // -1 if should skip
		internal FieldsDesc _HotFieldsDesc, _ColdFieldsDesc;

		internal List<(StringKey Name, FieldVal Value)>? _FloatingFields;

		public readonly int HotStoreLength => _HotStoreLength;
		public readonly int ColdStoreLength => _ColdStoreLength;

		public readonly List<(StringKey Name, FieldVal Value)>? FloatingFields => _FloatingFields;

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		public readonly void WriteHotStore(Stream destination) {
#if DEBUG
			Debug.Assert(_HotStoreLength > 0, $"Shouldn't be called while data length <= 0");
			long expectedEndPos = destination.Position + _HotStoreLength;
#endif
			FieldsDesc fDesc = _HotFieldsDesc;
			destination.WriteVarInt(fDesc);

			int fCount = fDesc.FieldCount;
			Debug.Assert(fCount <= _Offsets?.Length);
			Debug.Assert(fCount <= _Entries?.Length);

			// Get a reference to avoid unnecessary range checking
			ref int offsets_r0 = ref _Offsets.DangerousGetReference();
			{
				int fOffsetSize = fDesc.FOffsetSize;
				for (int i = 0; i < fCount; i++) {
					destination.WriteUInt32AsUIntX(
						(uint)U.Add(ref offsets_r0, i),
						fOffsetSize);
				}
			}

			// Get a reference to avoid unnecessary range checking
			ref var entries_r0 = ref _Entries.DangerousGetReference();
			for (int i = 0; i < fCount; i++) {
				ref var entry = ref U.Add(ref entries_r0, i);
				FieldVal? fval = entry.Override;
				if (fval == null) {
					entry.OrigValue.WriteTo(destination);
				} else {
					fval.WriteTo(destination);
				}
			}

#if DEBUG
			Debug.Assert(destination.Position == expectedEndPos);
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
		[SkipLocalsInit]
		public readonly void WriteColdStore(Stream destination) {
#if DEBUG
			Debug.Assert(_ColdStoreLength > 0, $"Shouldn't be called while data length <= 0");
			long expectedEndPos = destination.Position + _ColdStoreLength;
#endif
			FieldsDesc fDesc = _ColdFieldsDesc;
			destination.WriteVarInt(fDesc);

			int start = _HotFieldsDesc.FieldCount;
			int fCount = fDesc.FieldCount;
			Debug.Assert(start + fCount <= _Offsets?.Length);
			Debug.Assert(start + fCount <= _Entries?.Length);

			// Get a reference to avoid unnecessary range checking
			ref int offsets_r0 = ref _Offsets.DangerousGetReferenceAt(start);
			{
				int i = 0;
				if (i < fCount) {
					int offsetAdjustment = offsets_r0;
					int fOffsetSize = fDesc.FOffsetSize;
					do {
						destination.WriteUInt32AsUIntX(
							(uint)(U.Add(ref offsets_r0, i) - offsetAdjustment),
							fOffsetSize);
					} while (++i < fCount);
				}
			}

			// Get a reference to avoid unnecessary range checking
			ref var entries_r0 = ref _Entries.DangerousGetReferenceAt(start);
			for (int i = 0; i < fCount; i++) {
				ref var entry = ref U.Add(ref entries_r0, i);
				FieldVal? fval = entry.Override;
				if (fval == null) {
					entry.OrigValue.WriteTo(destination);
				} else {
					fval.WriteTo(destination);
				}
			}

#if DEBUG
			Debug.Assert(destination.Position == expectedEndPos);
#endif
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)] // Use fully optimizing JIT, right from the start!
		public void Dispose() {
			var entries = _Entries;
			if (entries != null) {
				ArrayPool<Entry>.Shared.ReturnClearingReferences(entries);
				_Entries = null!;

				ArrayPool<int>.Shared.Return(_Offsets, clearArray: false);
				_Offsets = null!;
			}
		}
	}
}

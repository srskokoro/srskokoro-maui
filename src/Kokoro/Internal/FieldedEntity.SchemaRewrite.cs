﻿namespace Kokoro.Internal;
using Blake2Fast.Implementation;
using Kokoro.Common.Util;
using Kokoro.Internal.Sqlite;

partial class FieldedEntity {
	internal const int MaxClassCount = byte.MaxValue;

	[SuppressMessage("Style", "IDE1006:Naming Styles")]
	private static class SchemaRewrite {

		internal static class DataWhenNoSharedFields {
			public static readonly byte[] ReadOnlyBytes;

			static DataWhenNoSharedFields() {
				const int length = FieldsDesc.VarIntLengthForEmpty;
				Debug.Assert(length == VarInts.LengthForZero);
				Debug.Assert(length == 1);

				var buffer = new byte[length] { 0 };
				Debug.Assert(buffer.SequenceEqual(VarInts.Bytes(FieldsDesc.Empty)));

				ReadOnlyBytes = buffer;
			}
		}

		internal struct ClassInfo {
			public long rowid;
			public UniqueId uid;
			public byte[] csum;
			public int ord;

			public ClassInfo(
				long rowid, UniqueId uid, byte[] csum, int ord
			) {
				this.rowid = rowid;
				this.uid = uid;
				this.csum = csum;
				this.ord = ord;
			}
		}

		internal struct FieldInfo {
			public long rowid;

			public int cls_ord;
			public FieldStoreType sto;
			public int ord;
			public FieldSpec old_idx_sto;

			public string name;
			public FieldVal? new_fval;
#if DEBUG
			public UniqueId cls_uid;
#endif

			public FieldInfo(
				long rowid,
				int cls_ord, FieldStoreType sto, int ord, FieldSpec old_idx_sto,
				string name, FieldVal? new_fval
#if DEBUG
				, UniqueId cls_uid
#endif
			) {
				this.rowid = rowid;

				this.cls_ord = cls_ord;
				this.sto = sto;
				this.ord = ord;
				this.old_idx_sto = old_idx_sto;

				this.name = name;
				this.new_fval = new_fval;
#if DEBUG
				this.cls_uid = cls_uid;
#endif
			}
		}

		internal sealed class Comparisons {
			private readonly List<FieldInfo> fldList;
			private readonly List<ClassInfo> clsList;

			public Comparisons(List<FieldInfo> fldList, List<ClassInfo> clsList) {
				this.fldList = fldList;
				this.clsList = clsList;
			}

			public int fldList_compare(byte x, byte y) {
				ref var r0 = ref fldList.AsSpan().DangerousGetReference();
				ref var a = ref U.Add(ref r0, x);
				ref var b = ref U.Add(ref r0, y);

				int cmp;
				// Partition the sorted array by field store type
				{
					// NOTE: Using `Enum.CompareTo()` has a boxing cost, which
					// sadly, JIT doesn't optimize out (for now). So we must
					// cast the enums to their int counterparts to avoid the
					// unnecessary box.
					var a_sto = (FieldStoreTypeInt)a.sto;
					var b_sto = (FieldStoreTypeInt)b.sto;
					cmp = a_sto.CompareTo(b_sto);
					if (cmp != 0) goto Return;
				}
				{
					cmp = a.cls_ord.CompareTo(b.cls_ord);
					if (cmp != 0) goto Return;
				}
				{
					cmp = a.ord.CompareTo(b.ord);
					if (cmp != 0) goto Return;
				}
				{
					Debug.Assert(a.rowid != b.rowid, $"Expecting no " +
						$"duplicates but found a duplicate field entry " +
						$"with name id {a.rowid}");
				}
				{
					cmp = string.CompareOrdinal(a.name, b.name);
					Debug.Assert(cmp != 0, $"Impossible! Two fields have " +
						$"different name ids ({a.rowid} and {b.rowid}) but " +
						$"same name: {a.name}");
				}
			Return:
				return cmp;
			}

			public int clsList_compare(byte x, byte y) {
				ref var r0 = ref clsList.AsSpan().DangerousGetReference();
				return U.Add(ref r0, x).uid.CompareTo(U.Add(ref r0, y).uid);
			}
		}
	}

	/// <remarks>
	/// <para>
	/// NOTE: This method will modify <see cref="_SchemaId"/> on successful
	/// return. If it's important that the old value of <see cref="_SchemaId"/>
	/// be preserved, perform a manual backup of the old value before the call.
	/// </para>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="NestingWriteTransaction"/>).
	/// <br/>- Must set <see cref="_SchemaId"/> beforehand to the rowid of the
	/// desired base schema.
	/// <br/>- Must have <paramref name="oldSchemaId"/> with the rowid of the
	/// actual schema being used by the <see cref="FieldedEntity">fielded entity</see>,
	/// as loaded beforehand while inside the transaction.
	/// <br/>- If <see cref="_SchemaId"/> != <paramref name="oldSchemaId"/>,
	/// then <see cref="FieldsReader.OverrideSharedStore(long)"><paramref name="fr"/>.OverrideSharedStore(<paramref name="oldSchemaId"/>)</see>
	/// must be called beforehand, at least once, while inside the transaction.
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private protected void RewriteSchema(long oldSchemaId, ref FieldsReader fr, ref FieldsWriter fw, int hotStoreLimit = DefaultHotStoreLimit) {
		// TODO Implement
	}

	[DoesNotReturn]
	private void E_TooManyClasses(int count) {
		Debug.Assert(count > MaxClassCount);
		throw new InvalidOperationException(
			$"Total number of classes (currently {count}) shouldn't exceed {MaxClassCount};" +
			$"{Environment.NewLine}Entity: {GetDebugLabel()};" +
			$"{Environment.NewLine}Base Schema: {_SchemaId};");
	}

	// --

	private const int SchemaUsumDigestLength = 30; // 240-bit hash

	private byte[] FinishWithSchemaUsum(ref Blake2bHashState hasher, byte fldLocalCount) {
		const int UsumVer = 1; // The version varint
		const int UsumVerLength = 1; // The varint length is a single byte for now
		Debug.Assert(VarInts.Length(UsumVer) == UsumVerLength);
		Debug.Assert(VarInts.Bytes(UsumVer)[0] == UsumVer);

		const int ExtraBytesNeeded = 1; // To encode `fldLocalCount`

		const int UsumPrefixLength = UsumVerLength + ExtraBytesNeeded;
		Span<byte> usum = stackalloc byte[UsumPrefixLength + SchemaUsumDigestLength];

		usum[0] = UsumVer; // Prepend version varint
		usum[1] = fldLocalCount;

		hasher.Finish(usum[UsumPrefixLength..]);

		// TODO In the future, once we're either using `sqlite3_stmt` directly or have replaced `Microsoft.Data.Sqlite`
		// with a custom version more suited to our needs, rent/stackalloc a buffer for the hash output instead, then
		// pass that as a `ReadOnlySpan<byte>` to `sqlite3_bind_blob()`.
		return usum.ToArray();
	}
}

namespace Kokoro.Internal;
using Blake2Fast.Implementation;
using Kokoro.Common.Util;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;

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
		DAssert_FieldsWriterPriorRewrite(ref fw);

		var clsSet = _Classes;
		// ^- NOTE: Soon, the class set will contain only the newly added
		// classes (i.e., classes awaiting addition). The code after will make
		// sure that happens. Later, it'll also include direct classes from the
		// base schema, provided that those awaiting removal are filtered out.
		// Afterwards, it'll also include the indirect classes, as included by
		// the classes in the current set. In the end, the resulting set will
		// contain all the classes of the fielded entity under a new schema.

		KokoroSqliteDb db;
		{
			// Reinitialize the class set with only the classes awaiting
			// addition, while also obtaining the class change set
			// --

			HashSet<long> clsChgSet; // The class change set

			if (clsSet != null) {
				clsChgSet = clsSet.Changes!;
				if (clsChgSet == null) {
					// NOTE: The favored case is schema rewrites due to shared
					// field changes, often without any class changes.
					clsSet.Clear();
					goto FallbackForClsChgSet;
				} else {
					// NOTE: The intersection represents the newly added
					// classes. Classes present in the change set but not in the
					// resulting set, represent the classes awaiting removal.
					clsSet.IntersectWith(clsChgSet);
					goto DoneWithClsChgSet;
				}
			} else {
				_Classes = clsSet = new();
			}
		FallbackForClsChgSet:
			clsChgSet = clsSet;
		DoneWithClsChgSet:
			;

			// Get the base schema's direct classes
			// --

			db = fr.Db;
			using var cmd = db.CreateCommand();
			cmd.Set($"SELECT cls FROM {Prot.SchemaToClass} WHERE (ind,schema)=(0,$schema)")
				.AddParams(new("$schema", _SchemaId));

			using var r = cmd.ExecuteReader();
			while (r.Read()) {
				r.DAssert_Name(0, "cls");
				long cls = r.GetInt64(0);

				if (!clsChgSet.Contains(cls))
					clsSet.Add(cls);
			}
		}

		int dclsCount = clsSet.Count;
		if (dclsCount > MaxClassCount) goto E_TooManyClasses;

		List<(long RowId, UniqueId Uid, byte[] Csum)> clsList = new(dclsCount);

		foreach (long rowid in clsSet) {
			clsList.Add((RowId: rowid, Uid: default, Csum: null!));
		}

		// --
		{
			SqliteParameter cmd_rowid = new() { ParameterName = "$rowid" };

			using var clsCmd = db.CreateCommand();
			clsCmd.Set($"SELECT uid,csum FROM {Prot.Class} WHERE rowid=$rowid")
				.AddParams(cmd_rowid);

			using var inclCmd = db.CreateCommand();
			inclCmd.Set($"SELECT incl FROM {Prot.ClassToInclude} WHERE cls=$rowid")
				.AddParams(cmd_rowid);

			for (int i = 0; i < clsList.Count; i++) {
				ref var cls = ref clsList.AsSpan().DangerousGetReferenceAt(i);
				cmd_rowid.Value = cls.RowId;

				// Get the needed class info
				using (var r = clsCmd.ExecuteReader()) {
					if (r.Read()) {
						r.DAssert_Name(0, "uid");
						cls.Uid = r.GetUniqueId(0);

						r.DAssert_Name(1, "csum");
						cls.Csum = r.GetBytes(1);
					} else {
						// The user probably attached a nonexistent class to the
						// fielded entity. Ignore it then.
					}
				}

				// Get the included classes, adding them as indirect classes
				using (var r = inclCmd.ExecuteReader()) {
					while (r.Read()) {
						r.DAssert_Name(0, "incl");
						long incl = r.GetInt64(0);
						if (clsSet.Add(incl)) {
							clsList.Add((RowId: incl, Uid: default, Csum: null!));
						}
					}
				}
			}
		}

		if (clsSet.Count > MaxClassCount) goto E_TooManyClasses;

		// TODO Implement
		throw new NotImplementedException("TODO");

	E_TooManyClasses:
		E_TooManyClasses(clsSet.Count);
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private static long InitBareSchema(Classes clsSet) {
		// TODO Implement
		throw new NotImplementedException("TODO");
	}

	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private static long InitFullSchema(long bareSchemaId, Classes? clsSet) {
		// TODO Implement
		throw new NotImplementedException("TODO");
	}

	private const int SchemaUsumDigestLength = 31; // 248-bit hash

	private byte[] FinishWithSchemaUsum(ref Blake2bHashState hasher, bool hasSharedData) {
		const int UsumVer = 1; // The version varint
		const int UsumVerLength = 1; // The varint length is a single byte for now
		Debug.Assert(VarInts.Length(UsumVer) == UsumVerLength);
		Debug.Assert(VarInts.Bytes(UsumVer)[0] == UsumVer);

		Span<byte> usum = stackalloc byte[UsumVerLength + SchemaUsumDigestLength];
		usum[0] = UsumVer; // Prepend version varint
		usum[1] = (byte)((usum[1] & unchecked((sbyte)0xFE)) | hasSharedData.ToByte());

		hasher.Finish(usum[UsumVerLength..]);

		// TODO In the future, once we're either using `sqlite3_stmt` directly or have replaced `Microsoft.Data.Sqlite`
		// with a custom version more suited to our needs, rent/stackalloc a buffer for the hash output instead, then
		// pass that as a `ReadOnlySpan<byte>` to `sqlite3_bind_blob()`.
		return usum.ToArray();
	}

	// --

	[DoesNotReturn]
	private void E_TooManyFields(int count) {
		Debug.Assert(count > MaxFieldCount);
		throw new InvalidOperationException(
			$"Total number of fields (currently {count}) shouldn't exceed {MaxFieldCount};" +
			$"{Environment.NewLine}Entity: {GetDebugLabel()};" +
			$"{Environment.NewLine}Base Schema: {_SchemaId};");
	}

	[DoesNotReturn]
	private void E_TooManyClasses(int count) {
		Debug.Assert(count > MaxClassCount);
		throw new InvalidOperationException(
			$"Total number of classes (currently {count}) shouldn't exceed {MaxClassCount};" +
			$"{Environment.NewLine}Entity: {GetDebugLabel()};" +
			$"{Environment.NewLine}Base Schema: {_SchemaId};");
	}
}

namespace Kokoro;
using Kokoro.Internal.Sqlite;
using System.Runtime.InteropServices;

partial class Class {

	private EnumGroups? _EnumGroups;

	private sealed class EnumGroups : Dictionary<StringKey, List<EnumInfo>?> {
		public EnumGroupChanges? Changes;
	}

	private sealed class EnumGroupChanges : Dictionary<StringKey, List<EnumInfo>?> { }


	public readonly struct EnumInfo {
		private readonly FieldVal _Value;
		private readonly int _Ordinal;

		public readonly FieldVal Value => _Value;
		public readonly int Ordinal => _Ordinal;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public EnumInfo(FieldVal value, int ordinal) {
			_Value = value;
			_Ordinal = ordinal;
		}
	}


	public ICollection<StringKey> EnumGroupNames {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _EnumGroups?.Keys ?? EmptyEnumGroupNames.Instance;
	}

	private static class EmptyEnumGroupNames {
		internal static readonly Dictionary<StringKey, List<EnumInfo>?>.KeyCollection Instance = new(new());
	}

	public void EnsureCachedEnumGroupName(StringKey name) {
		var enumGroups = _EnumGroups;
		if (enumGroups == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		enumGroups.TryAdd(name, null);
		return;

	Init:
		_EnumGroups = enumGroups = new();
		goto Set;
	}


	public bool TryGetEnumGroup(StringKey name, [MaybeNullWhen(false)] out List<EnumInfo> elems) {
		var enumGroups = _EnumGroups;
		if (enumGroups != null) {
			enumGroups.TryGetValue(name, out elems);
			return elems != null;
		}
		elems = null;
		return false;
	}

	public List<EnumInfo>? GetEnumGroup(StringKey name) {
		var enumGroups = _EnumGroups;
		if (enumGroups != null) {
			enumGroups.TryGetValue(name, out var elems);
			return elems;
		}
		return null;
	}

	public void SetEnumGroup(StringKey name, List<EnumInfo>? elems) {
		var enumGroups = _EnumGroups;
		if (enumGroups == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		var changes = enumGroups.Changes;
		if (changes == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanges;
		}

	Set:
		enumGroups[name] = elems;
		changes[name] = elems;
		return;

	Init:
		_EnumGroups = enumGroups = new();
	InitChanges:
		enumGroups.Changes = changes = new();
		goto Set;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void DeleteEnumGroup(StringKey name) => SetEnumGroup(name, null);

	/// <seealso cref="SetEnumGroupAsLoaded(StringKey, List{EnumInfo}?)"/>
	[SkipLocalsInit]
	public void SetCachedEnumGroup(StringKey name, List<EnumInfo>? elems) {
		var enumGroups = _EnumGroups;
		if (enumGroups == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		enumGroups[name] = elems;

		{
			var changes = enumGroups.Changes;
			// Optimized for the common case
			if (changes == null) {
				return;
			} else {
				ref var elemsRef = ref CollectionsMarshal.GetValueRefOrNullRef(changes, name);
				if (!U.IsNullRef(ref elemsRef)) {
					elemsRef = elems;
				}
				return;
			}
		}

	Init:
		_EnumGroups = enumGroups = new();
		goto Set;
	}

	/// <summary>
	/// Same as <see cref="UnmarkEnumGroupAsChanged(StringKey)"/> followed by
	/// <see cref="SetCachedEnumGroup(StringKey, List{EnumInfo}?)"/>.
	/// </summary>
	public void SetEnumGroupAsLoaded(StringKey name, List<EnumInfo>? elems) {
		var enumGroups = _EnumGroups;
		if (enumGroups == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		enumGroups.Changes?.Remove(name);

	Set:
		enumGroups[name] = elems;
		return;

	Init:
		_EnumGroups = enumGroups = new();
		goto Set;
	}


	public void UnmarkEnumGroupAsChanged(StringKey name)
		=> _EnumGroups?.Changes?.Remove(name);

	public void UnmarkEnumGroupsAsChanged()
		=> _EnumGroups?.Changes?.Clear();


	public void UnloadEnumGroup(StringKey name) {
		var enumGroups = _EnumGroups;
		if (enumGroups != null) {
			enumGroups.Changes?.Remove(name);
			enumGroups.Remove(name);
		}
	}

	public void UnloadEnumGroups() {
		var enumGroups = _EnumGroups;
		if (enumGroups != null) {
			enumGroups.Changes = null;
			enumGroups.Clear();
		}
	}

	// --

	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="OptionalReadTransaction"/>
	/// or <see cref="NestingWriteTransaction"/>).
	/// <br/>- Must call <see cref="KokoroSqliteDb.ReloadNameIdCaches()"/>
	/// beforehand, at least once, while inside the transaction.
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[SkipLocalsInit]
	private void InternalLoadEnumGroup(KokoroSqliteDb db, StringKey name) {
		long enmGrp = db.LoadStaleNameId(name);
		if (enmGrp == 0) goto NotFound;

		// Load enum group
		using (var cmd = db.CreateCommand()) {
			cmd.Set(
				$"SELECT type,data,ord\n" +
				$"FROM {Prot.ClassToEnum}\n" +
				$"WHERE cls=$cls AND enmGrp=$enmGrp"
			).AddParams(
				new("$cls", _RowId),
				new("$enmGrp", enmGrp)
			);

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				List<EnumInfo> elems = new();

				// Pending changes will be discarded
				SetEnumGroupAsLoaded(name, elems);

				do {
					r.DAssert_Name(0, "type");
					FieldTypeHint typeHint = (FieldTypeHint)r.GetInt64(0);

					FieldVal fval;
					if (typeHint != FieldTypeHint.Null) {
						r.DAssert_Name(1, "data");
						var data = r.GetBytes(1);

						fval = new(typeHint, data);
					} else {
						fval = FieldVal.Null;
					}

					r.DAssert_Name(2, "ord");
					int ordinal = r.GetInt32(2);

					EnumInfo info = new(fval, ordinal);
					elems.Add(info);
				} while (r.Read());

				return; // Early exit
			}
		}

	NotFound:
		// Otherwise, either deleted or never existed.
		// Let that state materialize here then.
		UnloadEnumGroup(name);
	}

	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="OptionalReadTransaction"/>
	/// or <see cref="NestingWriteTransaction"/>).
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[SkipLocalsInit]
	private void InternalLoadEnumGroups(KokoroSqliteDb db) {
		db.ReloadNameIdCaches(); // Needed by `db.LoadStale…()` below

		using var cmd = db.CreateCommand();
		cmd.Set(
			$"SELECT enmGrp,type,data,ord\n" +
			$"FROM {Prot.ClassToEnum}\n" +
			$"WHERE cls=$cls\n" +
			$"ORDER BY enmGrp"
		).AddParams(new("$cls", _RowId));

		using var r = cmd.ExecuteReader();

		var enumGroups = _EnumGroups;
		if (enumGroups == null) {
			_EnumGroups = enumGroups = new();
		} else {
			// Pending changes will be discarded
			enumGroups.Changes = null;
			enumGroups.Clear();
		}

		long prevEnmGrp = 0;
		List<EnumInfo> elems = null!;

		while (r.Read()) {
			r.DAssert_Name(0, "enmGrp");
			long enmGrp = r.GetInt64(0);
			if (enmGrp != prevEnmGrp) {
				prevEnmGrp = enmGrp;

				var name = db.LoadStaleName(enmGrp);
				Debug.Assert(name is not null, "An FK constraint should've " +
					"been enforced to ensure this doesn't happen.");

				elems = new();
				enumGroups[name] = elems;
			}

			r.DAssert_Name(1, "type");
			FieldTypeHint typeHint = (FieldTypeHint)r.GetInt64(1);

			FieldVal fval;
			if (typeHint != FieldTypeHint.Null) {
				r.DAssert_Name(2, "data");
				var data = r.GetBytes(2);

				fval = new(typeHint, data);
			} else {
				fval = FieldVal.Null;
			}

			r.DAssert_Name(3, "ord");
			int ordinal = r.GetInt32(3);

			EnumInfo info = new(fval, ordinal);
			elems.Add(info);
		}
	}

	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="OptionalReadTransaction"/>
	/// or <see cref="NestingWriteTransaction"/>).
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[SkipLocalsInit]
	private void InternalLoadEnumGroupNames(KokoroSqliteDb db) {
		db.ReloadNameIdCaches(); // Needed by `db.LoadStale…()` below

		using var cmd = db.CreateCommand();
		cmd.Set($"SELECT enmGrp FROM {Prot.ClassToEnum} WHERE cls=$cls")
			.AddParams(new("$cls", _RowId));

		using var r = cmd.ExecuteReader();
		while (r.Read()) {
			long enmGrp = r.GetInt64(0);
			var name = db.LoadStaleName(enmGrp);
			Debug.Assert(name is not null, "An FK constraint should've been " +
				"enforced to ensure this doesn't happen.");
			EnsureCachedEnumGroupName(name);
		}
	}

	// --

	public void LoadEnumGroup(StringKey enumGroup1) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadEnumGroup(db, enumGroup1);
			}
		}
	}

	public void LoadEnumGroup(StringKey enumGroup1, StringKey enumGroup2) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadEnumGroup(db, enumGroup1);
				InternalLoadEnumGroup(db, enumGroup2);
			}
		}
	}

	public void LoadEnumGroup(StringKey enumGroup1, StringKey enumGroup2, StringKey enumGroup3) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadEnumGroup(db, enumGroup1);
				InternalLoadEnumGroup(db, enumGroup2);
				InternalLoadEnumGroup(db, enumGroup3);
			}
		}
	}

	public void LoadEnumGroup(StringKey enumGroup1, StringKey enumGroup2, StringKey enumGroup3, StringKey enumGroup4) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				InternalLoadEnumGroup(db, enumGroup1);
				InternalLoadEnumGroup(db, enumGroup2);
				InternalLoadEnumGroup(db, enumGroup3);
				InternalLoadEnumGroup(db, enumGroup4);
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void LoadEnumGroup(params StringKey[] enumGroups)
		=> LoadEnumGroup(enumGroups.AsSpan());

	public void LoadEnumGroup(ReadOnlySpan<StringKey> enumGroups) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				db.ReloadNameIdCaches();
				foreach (var enumGroup in enumGroups)
					InternalLoadEnumGroup(db, enumGroup);
			}
		}
	}

	public void LoadEnumGroups() {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists)
				InternalLoadEnumGroups(db);
		}
	}

	public void LoadEnumGroupNames() {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists)
				InternalLoadEnumGroupNames(db);
		}
	}
}

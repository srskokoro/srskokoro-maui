namespace Kokoro;
using Kokoro.Internal.Sqlite;
using Microsoft.Data.Sqlite;

partial class Class {
	/// <summary>
	/// The set of classes held as class includes, to be indirectly included to
	/// the schema when the holding entity class is included directly.
	/// </summary>
	private Includes? _Includes;

	private sealed class Includes : HashSet<long> {
		/// <summary>
		/// The set of class includes marked as changed, a.k.a., class include
		/// change set.
		/// </summary>
		/// <remarks>
		/// RULES:
		/// <br/>- If a class include <b>is held</b> while marked as <b>changed</b>,
		/// the "changed" mark should be interpreted as the class awaiting <b>addition</b>
		/// to the holding entity class's list of includes.
		/// <br/>- If a class <b>is not held</b> while marked as <b>changed</b>,
		/// the "changed" mark should be interpreted as the class awaiting <b>removal</b>
		/// from the holding entity class's list of includes.
		/// </remarks>
		internal IncludeChanges? _Changes;
	}

	private sealed class IncludeChanges : HashSet<long> { }


	public bool IsIncludedAsCached(long classId) {
		var includes = _Includes;
		if (includes != null && includes.Contains(classId)) {
			return true;
		}
		return false;
	}

	public void AddInclude(long classId) {
		var includes = _Includes;
		if (includes == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		var changes = includes._Changes;
		if (changes == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanges;
		}

	Set:
		// NOTE: Mark first as held before marking as changed. Marking the class
		// as held may fail, and if that happens while the class is marked first
		// as changed, the "changed" mark would be interpreted incorrectly as a
		// "class removal" instead, which isn't what we want.
		includes.Add(classId); // Marks as held
		changes.Add(classId); // Marks as changed
		return;

	Init:
		_Includes = includes = new();
	InitChanges:
		includes._Changes = changes = new();
		goto Set;
	}

	public void RemoveInclude(long classId) {
		var includes = _Includes;
		if (includes == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		// NOTE: Unmark first as held before marking as changed. Unmarking the
		// class as held may fail, and if that happens while the class is held
		// and marked first as changed, the "changed" mark would be interpreted
		// incorrectly as a "class addition" instead, which isn't what we want.
		includes.Remove(classId); // Unmarks as held (if marked held before)

		var changes = includes._Changes;
		if (changes == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanges;
		}

	Set:
		changes.Add(classId); // Marks as changed
		return;

	Init:
		_Includes = includes = new();
	InitChanges:
		includes._Changes = changes = new();
		goto Set;
	}

	/// <seealso cref="AddIncludeAsLoaded(long)"/>
	public void AddIncludeToCache(long classId) {
		var includes = _Includes;
		if (includes == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		includes.Add(classId); // Marks as held
		return;

	Init:
		_Includes = includes = new();
		goto Set;
	}

	/// <summary>
	/// Same as <see cref="UnmarkIncludeAsChanged(long)"/> followed by
	/// <see cref="AddIncludeToCache(long)"/>.
	/// </summary>
	public void AddIncludeAsLoaded(long classId) {
		var includes = _Includes;
		if (includes == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		includes._Changes?.Remove(classId); // Unmarks as changed

	Set:
		includes.Add(classId); // Marks as held
		return;

	Init:
		_Includes = includes = new();
		goto Set;
	}

	/// <summary>
	/// Alias for <see cref="UnloadInclude(long)"/>
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RemoveIncludeFromCache(long classId)
		=> UnloadInclude(classId);


	public void UnmarkIncludeAsChanged(long classId)
		=> _Includes?._Changes?.Remove(classId);

	public void UnmarkIncludesAsChanged()
		=> _Includes?._Changes?.Clear();


	public void UnloadInclude(long classId) {
		var includes = _Includes;
		if (includes != null) {
			// NOTE: Unmark first as changed before unmarking as held. Unmarking
			// the class as changed may fail, and if that happens while the
			// class is unmarked first as held, the "changed" mark would be
			// interpreted incorrectly as a "class removal" -- a potentially
			// destructive side-effect.
			includes._Changes?.Remove(classId); // Unmarks as changed
			includes.Remove(classId); // Unmarks as held
		}
	}

	public void UnloadIncludes() {
		var includes = _Includes;
		if (includes != null) {
			includes._Changes = null; // Unmarks as changed
			includes.Clear(); // Unmarks as held
		}
	}

	// --

	/// <summary>
	/// Caches the specified class rowid if the current entity class possesses
	/// that as its class include.
	/// </summary>
	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="OptionalReadTransaction"/>
	/// or <see cref="NestingWriteTransaction"/>).
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[SkipLocalsInit]
	private bool InternalLoadInclude(KokoroSqliteDb db, long classId) {
		using var cmd = db.CreateCommand();
		cmd.Set(
			$"SELECT 1 FROM ClassToInclude\n" +
			$"WHERE (cls,incl)=($cls,$incl)"
		).AddParams(
			new("$cls", _RowId),
			new("$incl", classId)
		);

		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			AddIncludeAsLoaded(classId);
			return true;
		}

		return false;
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
	private void InternalLoadIncludes(KokoroSqliteDb db) {
		var includes = _Includes;
		if (includes == null) {
			_Includes = includes = new();
		} else {
			includes._Changes = null; // Unmarks as changed
			includes.Clear(); // Unmarks as held
		}

		using var cmd = db.CreateCommand();
		cmd.Set($"SELECT incl FROM ClassToInclude WHERE cls=$cls")
			.AddParams(new("$cls", _RowId));

		var r = cmd.ExecuteReader();
		r.DAssert_Name(0, "incl");

		while (r.Read()) {
			long incl = r.GetInt64(0);
			includes.Add(incl); // Marks as held
		}
	}

	// --

	public bool LoadInclude(long classId) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				return InternalLoadInclude(db, classId);
			}
			return false;
		}
	}

	public bool LoadInclude(long classId1, long classId2) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				return
					InternalLoadInclude(db, classId1) &
					InternalLoadInclude(db, classId2)
					;
			}
			return false;
		}
	}

	public bool LoadInclude(long classId1, long classId2, long classId3) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				return
					InternalLoadInclude(db, classId1) &
					InternalLoadInclude(db, classId2) &
					InternalLoadInclude(db, classId3)
					;
			}
			return false;
		}
	}

	public bool LoadInclude(long classId1, long classId2, long classId3, long classId4) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				return
					InternalLoadInclude(db, classId1) &
					InternalLoadInclude(db, classId2) &
					InternalLoadInclude(db, classId3) &
					InternalLoadInclude(db, classId4)
					;
			}
			return false;
		}
	}

	public bool LoadInclude(long classId1, long classId2, long classId3, long classId4, long classId5) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				return
					InternalLoadInclude(db, classId1) &
					InternalLoadInclude(db, classId2) &
					InternalLoadInclude(db, classId3) &
					InternalLoadInclude(db, classId4) &
					InternalLoadInclude(db, classId5)
					;
			}
			return false;
		}
	}

	public bool LoadInclude(long classId1, long classId2, long classId3, long classId4, long classId5, long classId6) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				return
					InternalLoadInclude(db, classId1) &
					InternalLoadInclude(db, classId2) &
					InternalLoadInclude(db, classId3) &
					InternalLoadInclude(db, classId4) &
					InternalLoadInclude(db, classId5) &
					InternalLoadInclude(db, classId6)
					;
			}
			return false;
		}
	}

	public bool LoadInclude(long classId1, long classId2, long classId3, long classId4, long classId5, long classId6, long classId7) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				return
					InternalLoadInclude(db, classId1) &
					InternalLoadInclude(db, classId2) &
					InternalLoadInclude(db, classId3) &
					InternalLoadInclude(db, classId4) &
					InternalLoadInclude(db, classId5) &
					InternalLoadInclude(db, classId6) &
					InternalLoadInclude(db, classId7)
					;
			}
			return false;
		}
	}

	public bool LoadInclude(long classId1, long classId2, long classId3, long classId4, long classId5, long classId6, long classId7, long classId8) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				return
					InternalLoadInclude(db, classId1) &
					InternalLoadInclude(db, classId2) &
					InternalLoadInclude(db, classId3) &
					InternalLoadInclude(db, classId4) &
					InternalLoadInclude(db, classId5) &
					InternalLoadInclude(db, classId6) &
					InternalLoadInclude(db, classId7) &
					InternalLoadInclude(db, classId8)
					;
			}
			return false;
		}
		// TODO A counterpart that loads up to 16 includes
		// TODO Generate code via T4 text templates instead
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool LoadInclude(params long[] classIds)
		=> LoadInclude(classIds.AsSpan());

	public bool LoadInclude(ReadOnlySpan<long> classIds) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists) {
				bool isWithGivenIncludes = true;

				foreach (var classId in classIds)
					isWithGivenIncludes &= InternalLoadInclude(db, classId);

				return isWithGivenIncludes;
			}
			return false;
		}
	}

	public void LoadIncludes() {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			if (Exists)
				InternalLoadIncludes(db);
		}
	}

	// --

	[SkipLocalsInit]
	private static void InternalSaveIncludes(KokoroSqliteDb db, Includes includes, long clsId) {
		var changes = includes._Changes;
		Debug.Assert(changes != null);

		var changes_iter = changes.GetEnumerator();
		if (!changes_iter.MoveNext()) goto NoChanges;

		SqliteCommand?
			insCmd = null,
			delCmd = null;

		SqliteParameter
			cmd_cls = new("$cls", clsId),
			cmd_incl = new() { ParameterName = "$incl" };

		try {
			do {
				long incl = changes_iter.Current;
				cmd_incl.Value = incl;

				if (includes.Contains(incl)) {
					if (insCmd != null) {
						goto InsertInclude;
					} else
						goto InitToInsertInclude;
				} else {
					if (delCmd != null) {
						goto DeleteInclude;
					} else
						goto InitToDeleteInclude;
				}

			InitToInsertInclude:
				{
					insCmd = db.CreateCommand();
					// NOTE: `INSERT … ON CONFLICT DO NOTHING` ignores rows that
					// violate uniqueness constraints only (unlike `INSERT OR IGNORE …`)
					insCmd.Set(
						$"INSERT INTO ClassToInclude(cls,incl)\n" +
						$"VALUES($cls,$incl)\n" +
						$"ON CONFLICT DO NOTHING"
					).AddParams(
						cmd_cls, cmd_incl
					);
					Debug.Assert(
						cmd_cls.Value != null &&
						cmd_incl.Value != null
					);
					goto InsertInclude;
				}

			InsertInclude:
				{
					int inserted = insCmd.ExecuteNonQuery();
					// NOTE: It's possible for nothing to be inserted, for when
					// the class include already exists.
					Debug.Assert(inserted is 1 or 0);
					continue;
				}

			InitToDeleteInclude:
				{
					delCmd = db.CreateCommand();
					delCmd.Set(
						$"DELETE FROM ClassToInclude\n" +
						$"WHERE (cls,incl)=($cls,$incl)"
					).AddParams(
						cmd_cls, cmd_incl
					);
					Debug.Assert(
						cmd_cls.Value != null &&
						cmd_incl.Value != null
					);
					goto DeleteInclude;
				}

			DeleteInclude:
				{
					int deleted = delCmd.ExecuteNonQuery();
					// NOTE: It's possible for nothing to be deleted, for when
					// the class include didn't exist in the first place.
					Debug.Assert(deleted is 1 or 0);
					continue;
				}

			} while (changes_iter.MoveNext());

		} finally {
			insCmd?.Dispose();
			delCmd?.Dispose();
		}

	NoChanges:
		;
	}
}

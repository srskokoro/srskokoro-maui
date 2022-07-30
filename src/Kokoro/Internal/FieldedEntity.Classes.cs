namespace Kokoro.Internal;
using Kokoro.Internal.Sqlite;

partial class FieldedEntity {
	/// <summary>
	/// The set of classes held by the <see cref="FieldedEntity">fielded entity</see>.
	/// </summary>
	private Classes? _Classes;

	private sealed class Classes : HashSet<long> {
		/// <summary>
		/// The set of classes marked as changed, a.k.a., class change set.
		/// </summary>
		/// <remarks>
		/// RULES:
		/// <br/>- If a class <b>is held</b> while marked as <b>changed</b>, the
		/// "changed" mark should be interpreted as the class awaiting <b>addition</b>
		/// to the new schema of the <see cref="FieldedEntity">fielded entity</see>.
		/// <br/>- If a class <b>is not held</b> while marked as <b>changed</b>,
		/// the "changed" mark should be interpreted as the class awaiting <b>removal</b>
		/// from the new schema, given that it existed in the old schema.
		/// </remarks>
		internal ClassChanges? _Changes;
	}

	private sealed class ClassChanges : HashSet<long> { }


	public bool IsOfClassCached(long classId) {
		var classes = _Classes;
		if (classes != null && classes.Contains(classId)) {
			return true;
		}
		return false;
	}

	public void AddClass(long classId) {
		var classes = _Classes;
		if (classes == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		var changes = classes._Changes;
		if (changes == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanges;
		}

	Set:
		// NOTE: Mark first as held before marking as changed. Marking the class
		// as held may fail, and if that happens while the class is marked first
		// as changed, the "changed" mark would be interpreted incorrectly as a
		// "class removal" instead, which isn't what we want.
		classes.Add(classId); // Marks as held
		changes.Add(classId); // Marks as changed
		return;

	Init:
		_Classes = classes = new();
	InitChanges:
		classes._Changes = changes = new();
		goto Set;
	}

	public void RemoveClass(long classId) {
		var classes = _Classes;
		if (classes == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		// NOTE: Unmark first as held before marking as changed. Unmarking the
		// class as held may fail, and if that happens while the class is held
		// and marked first as changed, the "changed" mark would be interpreted
		// incorrectly as a "class addition" instead, which isn't what we want.
		classes.Remove(classId); // Unmarks as held (if marked held before)

		var changes = classes._Changes;
		if (changes == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanges;
		}

	Set:
		changes.Add(classId); // Marks as changed
		return;

	Init:
		_Classes = classes = new();
	InitChanges:
		classes._Changes = changes = new();
		goto Set;
	}

	/// <seealso cref="AddClassAsLoaded(long)"/>
	public void AddClassToCache(long classId) {
		var classes = _Classes;
		if (classes == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		classes.Add(classId); // Marks as held
		return;

	Init:
		_Classes = classes = new();
		goto Set;
	}

	/// <summary>
	/// Same as <see cref="UnmarkClassAsChanged(long)"/> followed by
	/// <see cref="AddClassToCache(long)"/>.
	/// </summary>
	public void AddClassAsLoaded(long classId) {
		var classes = _Classes;
		if (classes == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		classes._Changes?.Remove(classId); // Unmarks as changed

	Set:
		classes.Add(classId); // Marks as held
		return;

	Init:
		_Classes = classes = new();
		goto Set;
	}

	/// <summary>
	/// Alias for <see cref="UnloadClassId(long)"/>
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void RemoveClassFromCache(long classId)
		=> UnloadClassId(classId);


	public void UnmarkClassAsChanged(long classId)
		=> _Classes?._Changes?.Remove(classId);

	public void UnmarkClassesAsChanged() {
		var classes = _Classes;
		if (classes != null)
			classes._Changes = null;
	}


	public void UnloadClassId(long classId) {
		var classes = _Classes;
		if (classes != null) {
			// NOTE: Unmark first as changed before unmarking as held. Unmarking
			// the class as changed may fail, and if that happens while the
			// class is unmarked first as held, the "changed" mark would be
			// interpreted incorrectly as a "class removal" -- a potentially
			// destructive side-effect.
			classes._Changes?.Remove(classId); // Unmarks as changed
			classes.Remove(classId); // Unmarks as held
		}
	}

	public void UnloadClassIds() {
		var classes = _Classes;
		if (classes != null) {
			classes._Changes = null; // Unmarks as changed
			classes.Clear(); // Unmarks as held
		}
	}

	// --

	/// <summary>
	/// Caches the specified class rowid if the fielded entity possesses the
	/// class.
	/// </summary>
	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="OptionalReadTransaction"/>
	/// or <see cref="NestingWriteTransaction"/>).
	/// <br/>- Must load <see cref="_SchemaId"/> beforehand, at least once,
	/// while inside the transaction.
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[SkipLocalsInit]
	private protected bool InternalLoadClassId(KokoroSqliteDb db, long classId) {
		using var cmd = db.CreateCommand();
		cmd.Set(
			"SELECT 1 FROM SchemaToClass\n" +
			"WHERE (schema,cls)=($schema,$cls)"
		).AddParams(
			new("$schema", _SchemaId),
			new("$cls", classId)
		);

		using var r = cmd.ExecuteReader();
		if (r.Read()) {
			AddClassAsLoaded(classId);
			return true;
		}

		return false;
	}

	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="OptionalReadTransaction"/>
	/// or <see cref="NestingWriteTransaction"/>).
	/// <br/>- Must load <see cref="_SchemaId"/> beforehand, at least once,
	/// while inside the transaction.
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[SkipLocalsInit]
	private protected void InternalLoadClassIds(KokoroSqliteDb db) {
		var classes = _Classes;
		if (classes == null) {
			_Classes = classes = new();
		} else {
			classes._Changes = null; // Unmarks as changed
			classes.Clear(); // Unmarks as held
		}

		using var cmd = db.CreateCommand();
		cmd.Set("SELECT cls FROM SchemaToClass WHERE schema=$schema")
			.AddParams(new("$schema", _SchemaId));

		var r = cmd.ExecuteReader();
		r.DAssert_Name(0, "cls");

		while (r.Read()) {
			long cls = r.GetInt64(0);
			classes.Add(cls); // Marks as held
		}
	}
}

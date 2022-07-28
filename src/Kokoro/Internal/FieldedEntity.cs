namespace Kokoro.Internal;
using Kokoro.Common.Sqlite;
using Kokoro.Internal.Sqlite;
using System.Runtime.InteropServices;

public abstract partial class FieldedEntity : DataEntity {

	private protected long _SchemaRowId;

	private Dictionary<StringKey, FieldVal>? _Fields;
	private Dictionary<StringKey, FieldVal>? _FieldChanges;

	public long SchemaRowId => _SchemaRowId;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal FieldedEntity(KokoroCollection host) : base(host) { }

	public void SetCachedSchemaRowId(long schemaRowId) => _SchemaRowId = schemaRowId;


	public bool TryGet(StringKey name, [MaybeNullWhen(false)] out FieldVal value) {
		var fields = _Fields;
		if (fields != null && fields.TryGetValue(name, out value)) {
			return true;
		}
		value = default;
		return false;
	}

	public void Set(StringKey name, FieldVal value) {
		var fields = _Fields;
		if (fields == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		var changes = _FieldChanges;
		if (changes == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanges;
		}

	Set:
		fields[name] = value;
		changes[name] = value;
		return;

	Init:
		_Fields = fields = new();
	InitChanges:
		_FieldChanges = changes = new();
		goto Set;
	}

	/// <seealso cref="SetAsLoaded(StringKey, FieldVal)"/>
	[SkipLocalsInit]
	public void SetCache(StringKey name, FieldVal value) {
		var fields = _Fields;
		if (fields == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

	Set:
		fields[name] = value;

		{
			var changes = _FieldChanges;
			// Optimized for the common case
			if (changes == null) {
				return;
			} else {
				ref var valueRef = ref CollectionsMarshal.GetValueRefOrNullRef(changes, name);
				if (!U.IsNullRef(ref valueRef)) {
					valueRef = value;
				}
				return;
			}
		}

	Init:
		_Fields = fields = new();
		goto Set;
	}

	/// <summary>
	/// Same as <see cref="ClearFieldChangeStatus(StringKey)"/> followed by
	/// <see cref="SetCache(StringKey, FieldVal)"/>.
	/// </summary>
	public void SetAsLoaded(StringKey name, FieldVal value) {
		var fields = _Fields;
		if (fields == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		_FieldChanges?.Remove(name);

	Set:
		fields[name] = value;
		return;

	Init:
		_Fields = fields = new();
		goto Set;
	}

	public void ClearFieldChangeStatus(StringKey name)
		=> _FieldChanges?.Remove(name);

	public void ClearFieldChangeStatuses()
		=> _FieldChanges = null;


	public void UnloadField(StringKey fieldName) {
		var fields = _Fields;
		if (fields != null) {
			fields.Remove(fieldName);
			_FieldChanges?.Remove(fieldName);
		}
	}

	public void UnloadFields() {
		var fields = _Fields;
		if (fields != null) {
			fields.Clear();
			_FieldChanges = null;
		}
	}

	// --

	internal abstract Stream ReadHotStore(KokoroSqliteDb db);

	internal virtual Stream ReadColdStore(KokoroSqliteDb db) => Stream.Null;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal Stream ReadSharedStore(KokoroSqliteDb db) {
		return SqliteBlobSlim.Open(db,
			tableName: "Schema", columnName: "data", rowid: _SchemaRowId,
			canWrite: false, throwOnAccessFail: false) ?? Stream.Null;
	}

	// --

	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="OptionalReadTransaction"/>
	/// or <see cref="NestingWriteTransaction"/>).
	/// <br/>- Must call <see cref="KokoroSqliteDb.ReloadFieldNameCaches()"/>
	/// beforehand, at least once, while inside the transaction.
	/// <br/>- Must load <see cref="_SchemaRowId"/> beforehand, at least once,
	/// while inside the transaction.
	/// <para>
	/// Violation of the above contract may result in undefined behavior.
	/// </para>
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private protected void InternalLoadField(ref FieldsReader fr, StringKey fieldName) {
		FieldVal? fval;
		FieldSpec fspec;

		var db = fr.Db;
		long fld = db.LoadStaleFieldId(fieldName);
		if (fld == 0) goto NotFound;

		using (var cmd = db.CreateCommand()) {
			cmd.Set(
				"SELECT idx_sto FROM SchemaToField\n" +
				"WHERE schema=$schema AND fld=$fld"
			);
			var cmdParams = cmd.Parameters;
			cmdParams.Add(new("$schema", _SchemaRowId));
			cmdParams.Add(new("$fld", fld));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				r.DAssert_Name(0, "idx_sto");
				fspec = r.GetInt32(0);
				Debug.Assert(fspec.Index >= 0);
				fspec.StoreType.DAssert_Defined();
				goto WithFieldSpec;
			} else {
				goto WithoutFieldSpec;
			}
		}

	WithFieldSpec:
		fval = fr.Read(fspec);

	Found:
		{
			// Pending changes will be discarded
			SetAsLoaded(fieldName, fval);
			return; // Early exit
		}

	NotFound:
		{
			// Otherwise, either deleted or never existed.
			// Let that state materialize here then.
			UnloadField(fieldName);
			return; // Early exit
		}

	WithoutFieldSpec:
		{
			fval = OnLoadFloatingField(db, fld);
			if (fval != null) {
				goto Found;
			} else {
				goto NotFound;
			}
		}
	}

	private protected abstract FieldVal? OnLoadFloatingField(KokoroSqliteDb db, long fieldId);

	private protected abstract FieldVal? OnSupplantFloatingField(KokoroSqliteDb db, long fieldId);

	// --

	internal abstract string GetDebugLabel();
}

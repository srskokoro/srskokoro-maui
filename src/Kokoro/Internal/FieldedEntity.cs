namespace Kokoro.Internal;
using Kokoro.Common.Sqlite;
using Kokoro.Internal.Sqlite;
using System.Runtime.InteropServices;

public abstract class FieldedEntity : DataEntity {

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
		U.SkipInit(out value);
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

	// --

	internal abstract Stream ReadHotStore(KokoroSqliteDb db);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal Stream ReadSharedStore(KokoroSqliteDb db) {
		// NOTE: It's possible for the schema to not exist (in that case, the
		// following will throw) due to either the schema rowid being invalid
		// (e.g., if the fielded entity is yet to load it) or the schema no
		// longer existing. If the latter, then it means that there are no more
		// references to the schema and it got deleted; additionally, this also
		// means that the fielded entity no longer exists.
		//
		// Any exception that may be thrown because of the above can be
		// prevented by either making sure that the schema rowid is valid (e.g.,
		// by loading the fielded entity first) or by not calling this method if
		// the fielded entity is found to not exist (either because it no longer
		// exists or it simply didn't exist to begin with).
		return SqliteBlobSlim.Open(db,
			tableName: "Schema", columnName: "data", rowid: _SchemaRowId,
			canWrite: false, throwOnAccessFail: true)!;
	}

	internal virtual Stream ReadColdStore(KokoroSqliteDb db) => Stream.Null;


	[MethodImpl(MethodImplOptions.AggressiveOptimization)]
	[SkipLocalsInit]
	private protected void InternalLoadField(ref FieldsReader fr, StringKey fieldName) {
		FieldVal? fval;
		FieldSpec fspec;

		var db = fr.Db;
		long fld = db.LoadStaleFieldId(fieldName);
		if (fld == 0) goto NotFound;

		using (var cmd = db.CreateCommand()) {
			cmd.Set("""
				SELECT idx_a_sto FROM SchemaToField
				WHERE schema=$schema AND fld=$fld
				""");

			var cmdParams = cmd.Parameters;
			cmdParams.Add(new("$schema", _SchemaRowId));
			cmdParams.Add(new("$fld", fld));

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				r.DAssert_Name(0, "idx_a_sto");
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

	private protected void UnloadField(StringKey fieldName) {
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

	internal abstract string GetDebugLabel();
}

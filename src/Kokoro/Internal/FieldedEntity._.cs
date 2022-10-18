namespace Kokoro.Internal;
using Kokoro.Common.Sqlite;
using Kokoro.Internal.Sqlite;
using System.Collections;
using System.Runtime.InteropServices;

public abstract partial class FieldedEntity : DataEntity, IEnumerable<KeyValuePair<StringKey, FieldVal>> {

	private protected long _SchemaId;
	private Fields? _Fields;

	private sealed class Fields : Dictionary<StringKey, FieldVal> {
		public FieldChanges? Changes;
	}

	private sealed class FieldChanges : Dictionary<StringKey, object> { }

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal FieldedEntity(KokoroCollection host) : base(host) { }


	public long SchemaId {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _SchemaId;
	}

	public void SetCachedSchemaId(long schemaId) => _SchemaId = schemaId;


	public bool TryGet(StringKey name, [MaybeNullWhen(false)] out FieldVal value) {
		var fields = _Fields;
		if (fields != null && fields.TryGetValue(name, out value)) {
			return true;
		}
		value = null;
		return false;
	}

	public FieldVal? Get(StringKey name) {
		var fields = _Fields;
		if (fields != null) {
			fields.TryGetValue(name, out var value);
			return value;
		}
		return null;
	}

	public void Set(StringKey name, FieldVal value) {
		var fields = _Fields;
		if (fields == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		var changes = fields.Changes;
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
		fields.Changes = changes = new();
		goto Set;
	}

	public void Set(StringKey name, StringKey valueSource) {
		var fields = _Fields;
		if (fields == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		var changes = fields.Changes;
		if (changes == null) {
			// This becomes a conditional jump forward to not favor it
			goto InitChanges;
		}

	Set:
		fields.Remove(name);
		changes[name] = valueSource;
		return;

	Init:
		_Fields = fields = new();
	InitChanges:
		fields.Changes = changes = new();
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
			var changes = fields.Changes;
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
	/// Same as <see cref="UnmarkFieldAsChanged(StringKey)"/> followed by
	/// <see cref="SetCache(StringKey, FieldVal)"/>.
	/// </summary>
	public void SetAsLoaded(StringKey name, FieldVal value) {
		var fields = _Fields;
		if (fields == null) {
			// This becomes a conditional jump forward to not favor it
			goto Init;
		}

		fields.Changes?.Remove(name);

	Set:
		fields[name] = value;
		return;

	Init:
		_Fields = fields = new();
		goto Set;
	}


	public void UnmarkFieldAsChanged(StringKey name)
		=> _Fields?.Changes?.Remove(name);

	public void UnmarkFieldsAsChanged()
		=> _Fields?.Changes?.Clear();


	public void UnloadField(StringKey fieldName) {
		var fields = _Fields;
		if (fields != null) {
			fields.Changes?.Remove(fieldName);
			fields.Remove(fieldName);
		}
	}

	public void UnloadFields() {
		var fields = _Fields;
		if (fields != null) {
			fields.Changes = null;
			fields.Clear();
		}
	}

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Enumerator GetEnumerator() => new(_Fields);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	IEnumerator<KeyValuePair<StringKey, FieldVal>> IEnumerable<KeyValuePair<StringKey, FieldVal>>.GetEnumerator() => GetEnumerator();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


	/// <remarks>
	/// When <see langword="true"/>, <see cref="MayCompileFieldChanges"/> will
	/// also be <see langword="true"/>.
	/// </remarks>
	private protected bool HasPendingFieldChanges {

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		get {
			var fields = _Fields;
			if (fields == null) goto NoChanges;

			var changes = fields.Changes;
			if (changes == null) goto NoChanges;
			if (changes.Count == 0) goto NoChanges;

			Debug.Assert(MayCompileFieldChanges);
			return true;

		NoChanges:
			return false;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public FieldsChangedEnumerable EnumerateFieldsChanged() => new(this);

	public readonly struct FieldsChangedEnumerable : IEnumerable<KeyValuePair<StringKey, object>> {
		private readonly FieldedEntity _Owner;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FieldsChangedEnumerable(FieldedEntity owner) => _Owner = owner;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FieldsChangedEnumerator GetEnumerator() => new(_Owner._Fields?.Changes);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		IEnumerator<KeyValuePair<StringKey, object>> IEnumerable<KeyValuePair<StringKey, object>>.GetEnumerator() => GetEnumerator();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}


	public struct Enumerator : IEnumerator<KeyValuePair<StringKey, FieldVal>> {
		private Dictionary<StringKey, FieldVal>.Enumerator _Impl;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Enumerator(Dictionary<StringKey, FieldVal>? fields)
			=> _Impl = (fields ?? EmptySource.Instance).GetEnumerator();

		private static class EmptySource {
			internal static readonly Dictionary<StringKey, FieldVal> Instance = new();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool MoveNext() => _Impl.MoveNext();

		public KeyValuePair<StringKey, FieldVal> Current {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _Impl.Current;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Dispose() => _Impl.Dispose();

		// --

		object? IEnumerator.Current {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _Impl.Current;
		}

		void IEnumerator.Reset() => throw new NotSupportedException();
	}

	public struct FieldsChangedEnumerator : IEnumerator<KeyValuePair<StringKey, object>> {
		private Dictionary<StringKey, object>.Enumerator _Impl;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FieldsChangedEnumerator(Dictionary<StringKey, object>? changes)
			=> _Impl = (changes ?? EmptySource.Instance).GetEnumerator();

		private static class EmptySource {
			internal static readonly Dictionary<StringKey, object> Instance = new();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool MoveNext() => _Impl.MoveNext();

		public KeyValuePair<StringKey, object> Current {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _Impl.Current;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Dispose() => _Impl.Dispose();

		// --

		object? IEnumerator.Current {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _Impl.Current;
		}

		void IEnumerator.Reset() => throw new NotSupportedException();
	}

	// --

	internal abstract Stream ReadHotStore(KokoroSqliteDb db);

	internal virtual Stream ReadColdStore(KokoroSqliteDb db) => Stream.Null;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal Stream ReadSharedStore(KokoroSqliteDb db) => ReadSharedStore(db, _SchemaId);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static Stream ReadSharedStore(KokoroSqliteDb db, long schemaId) {
		return SqliteBlobSlim.Open(db,
			tableName: Prot.Schema, columnName: "data", rowid: schemaId,
			canWrite: false, throwOnAccessFail: DEBUG) ?? Stream.Null;
	}

	// --

	/// <remarks>
	/// CONTRACT:
	/// <br/>- Must be called while inside a transaction (ideally, using <see cref="OptionalReadTransaction"/>
	/// or <see cref="NestingWriteTransaction"/>).
	/// <br/>- Must call <see cref="KokoroSqliteDb.ReloadNameIdCaches()"/>
	/// beforehand, at least once, while inside the transaction.
	/// <br/>- Must load <see cref="_SchemaId"/> beforehand, at least once,
	/// while inside the transaction.
	/// <br/>- Must load <see cref="_SchemaId"/> with the rowid of the actual
	/// schema being used by the <see cref="FieldedEntity">fielded entity</see>.
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
		long fld = db.LoadStaleNameId(fieldName);
		if (fld == 0) goto NotFound;

		using (var cmd = db.CreateCommand()) {
			cmd.Set(
				$"SELECT idx_e_sto FROM {Prot.SchemaToField}\n" +
				$"WHERE schema=$schema AND fld=$fld"
			).AddParams(
				new("$schema", _SchemaId),
				new("$fld", fld)
			);

			using var r = cmd.ExecuteReader();
			if (r.Read()) {
				r.DAssert_Name(0, "idx_e_sto");
				fspec = r.GetInt32(0);
				fspec.DAssert_Valid();
				goto WithFieldSpec;
			} else {
				goto WithoutFieldSpec;
			}
		}

	WithFieldSpec:
		// NOTE: Should resolve field enum value, if any.
		fval = fr.Read(fspec);
		Debug.Assert(fval.TypeHint != FieldTypeHint.Enum);

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

	[DoesNotReturn]
	private protected static void E_FloatingFieldDataTooLarge(KokoroSqliteDb db, uint currentSize) {
		long limit = SQLitePCL.raw.sqlite3_limit(db.Handle, SQLitePCL.raw.SQLITE_LIMIT_LENGTH, -1);
		throw new InvalidOperationException(
			$"Total number of bytes for floating field data (currently " +
			$"{currentSize}) caused the DB row to exceed the limit of {limit}" +
			$" bytes.");
	}

	// --

	internal abstract string GetDebugLabel();
}

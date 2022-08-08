namespace Kokoro;
using Kokoro.Internal.Sqlite;

partial class Item {

	public bool LoadClassId(long classId) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			LoadSchemaId(db);
			if (Exists) {
				return InternalLoadClassId(db, classId);
			}
			return false;
		}
	}

	public bool LoadClassId(long classId1, long classId2) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			LoadSchemaId(db);
			if (Exists) {
				return
					InternalLoadClassId(db, classId1) &
					InternalLoadClassId(db, classId2)
					;
			}
			return false;
		}
	}

	public bool LoadClassId(long classId1, long classId2, long classId3) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			LoadSchemaId(db);
			if (Exists) {
				return
					InternalLoadClassId(db, classId1) &
					InternalLoadClassId(db, classId2) &
					InternalLoadClassId(db, classId3)
					;
			}
			return false;
		}
	}

	public bool LoadClassId(long classId1, long classId2, long classId3, long classId4) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			LoadSchemaId(db);
			if (Exists) {
				return
					InternalLoadClassId(db, classId1) &
					InternalLoadClassId(db, classId2) &
					InternalLoadClassId(db, classId3) &
					InternalLoadClassId(db, classId4)
					;
			}
			return false;
		}
	}

	public bool LoadClassId(long classId1, long classId2, long classId3, long classId4, long classId5) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			LoadSchemaId(db);
			if (Exists) {
				return
					InternalLoadClassId(db, classId1) &
					InternalLoadClassId(db, classId2) &
					InternalLoadClassId(db, classId3) &
					InternalLoadClassId(db, classId4) &
					InternalLoadClassId(db, classId5)
					;
			}
			return false;
		}
	}

	public bool LoadClassId(long classId1, long classId2, long classId3, long classId4, long classId5, long classId6) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			LoadSchemaId(db);
			if (Exists) {
				return
					InternalLoadClassId(db, classId1) &
					InternalLoadClassId(db, classId2) &
					InternalLoadClassId(db, classId3) &
					InternalLoadClassId(db, classId4) &
					InternalLoadClassId(db, classId5) &
					InternalLoadClassId(db, classId6)
					;
			}
			return false;
		}
	}

	public bool LoadClassId(long classId1, long classId2, long classId3, long classId4, long classId5, long classId6, long classId7) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			LoadSchemaId(db);
			if (Exists) {
				return
					InternalLoadClassId(db, classId1) &
					InternalLoadClassId(db, classId2) &
					InternalLoadClassId(db, classId3) &
					InternalLoadClassId(db, classId4) &
					InternalLoadClassId(db, classId5) &
					InternalLoadClassId(db, classId6) &
					InternalLoadClassId(db, classId7)
					;
			}
			return false;
		}
	}

	public bool LoadClassId(long classId1, long classId2, long classId3, long classId4, long classId5, long classId6, long classId7, long classId8) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			LoadSchemaId(db);
			if (Exists) {
				return
					InternalLoadClassId(db, classId1) &
					InternalLoadClassId(db, classId2) &
					InternalLoadClassId(db, classId3) &
					InternalLoadClassId(db, classId4) &
					InternalLoadClassId(db, classId5) &
					InternalLoadClassId(db, classId6) &
					InternalLoadClassId(db, classId7) &
					InternalLoadClassId(db, classId8)
					;
			}
			return false;
		}
		// TODO A counterpart that loads up to 16 class ids
		// TODO Generate code via T4 text templates instead
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool LoadClassId(params long[] classIds)
		=> LoadClassId(classIds.AsSpan());

	public bool LoadClassId(ReadOnlySpan<long> classIds) {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			LoadSchemaId(db);
			if (Exists) {
				bool isOfGivenClasses = true;

				foreach (var classId in classIds)
					isOfGivenClasses &= InternalLoadClassId(db, classId);

				return isOfGivenClasses;
			}
			return false;
		}
	}

	public void LoadClassIds() {
		var db = Host.Db;
		using (new OptionalReadTransaction(db)) {
			LoadSchemaId(db);
			if (Exists)
				InternalLoadClassIds(db);
		}
	}
}

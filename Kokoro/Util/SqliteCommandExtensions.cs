using Microsoft.Data.Sqlite;

namespace Kokoro.Util;

internal static class SqliteCommandExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand In(this SqliteCommand command, SqliteTransaction? transaction) {
		command.Transaction = transaction;
		return command;
	}

	public static SqliteCommand Set(this SqliteCommand command, params SqliteParameter[] parameters) {
		var _parameters = command.Parameters;
		if (_parameters.Count > 0) {
			_parameters.Clear();
		}
		_parameters.AddRange(parameters);
		return command;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ExecuteScalar<T>(this SqliteCommand command)
		=> (T)command.ExecuteScalar()!;
}

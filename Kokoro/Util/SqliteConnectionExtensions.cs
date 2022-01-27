using Microsoft.Data.Sqlite;

namespace Kokoro.Util;

public static class SqliteConnectionExtensions {

	public static SqliteCommand CreateCommand(this SqliteConnection connection, string commandText) {
		var command = connection.CreateCommand();
		command.CommandText = commandText;
		return command;
	}

	public static SqliteCommand CreateCommand(this SqliteConnection connection, string commandText, params SqliteParameter[] parameters) {
		var command = connection.CreateCommand(commandText);
		command.Parameters.AddRange(parameters);
		return command;
	}


	public static int ExecuteNonQuery(this SqliteConnection connection, string commandText) {
		using var command = connection.CreateCommand(commandText);
		return command.ExecuteNonQuery();
	}

	public static int ExecuteNonQuery(this SqliteConnection connection, string commandText, params SqliteParameter[] parameters) {
		using var command = connection.CreateCommand(commandText, parameters);
		return command.ExecuteNonQuery();
	}


	public static T ExecuteScalar<T>(this SqliteConnection connection, string commandText)
		=> (T)ExecuteScalar(connection, commandText)!;

	public static T ExecuteScalar<T>(this SqliteConnection connection, string commandText, params SqliteParameter[] parameters)
		=> (T)ExecuteScalar(connection, commandText, parameters)!;


	public static object? ExecuteScalar(this SqliteConnection connection, string commandText) {
		using var command = connection.CreateCommand(commandText);
		return command.ExecuteScalar();
	}

	public static object? ExecuteScalar(this SqliteConnection connection, string commandText, params SqliteParameter[] parameters) {
		using var command = connection.CreateCommand(commandText, parameters);
		return command.ExecuteScalar();
	}
}

namespace Kokoro.Common.Sqlite.Extensions;
using Microsoft.Data.Sqlite;

internal static class SqliteConnectionExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteConnection OpenAndGet(this SqliteConnection connection) {
		connection.Open();
		return connection;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand CreateCommand(this SqliteConnection connection, string commandText) {
		var command = connection.CreateCommand();
		command.CommandText = commandText;
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
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


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ExecuteScalar<T>(this SqliteConnection connection, string commandText)
		=> (T)connection.ExecuteScalar(commandText)!;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ExecuteScalar<T>(this SqliteConnection connection, string commandText, params SqliteParameter[] parameters)
		=> (T)connection.ExecuteScalar(commandText, parameters)!;


	public static object? ExecuteScalar(this SqliteConnection connection, string commandText) {
		using var command = connection.CreateCommand(commandText);
		return command.ExecuteScalar();
	}

	public static object? ExecuteScalar(this SqliteConnection connection, string commandText, params SqliteParameter[] parameters) {
		using var command = connection.CreateCommand(commandText, parameters);
		return command.ExecuteScalar();
	}
}

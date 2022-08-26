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
	public static SqliteCommand Cmd(this SqliteConnection connection) {
		return connection.CreateCommand();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand Cmd(this SqliteConnection connection, string commandText) {
		return connection.CreateCommand(commandText);
	}


	public static int ExecuteNonQuery(this SqliteConnection connection, string commandText) {
		using var command = connection.CreateCommand(commandText);
		return command.ExecuteNonQuery();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ExecuteScalar<T>(this SqliteConnection connection, string commandText)
		=> (T)connection.ExecuteScalar(commandText)!;

	public static object? ExecuteScalar(this SqliteConnection connection, string commandText) {
		using var command = connection.CreateCommand(commandText);
		return command.ExecuteScalar();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T? ExecuteScalarOrDefaultIfEmpty<T>(this SqliteConnection connection, string commandText) {
		object? obj = connection.ExecuteScalar(commandText);
		if (obj != null) return (T)obj;
		return default;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Exec(this SqliteConnection connection, string commandText)
		=> connection.ExecuteNonQuery(commandText);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ExecScalar<T>(this SqliteConnection connection, string commandText)
		=> (T)connection.ExecuteScalar(commandText)!;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static object? ExecScalar(this SqliteConnection connection, string commandText)
		=> connection.ExecuteScalar(commandText);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T? ExecScalarOrDefaultIfEmpty<T>(this SqliteConnection connection, string commandText)
		=> connection.ExecuteScalarOrDefaultIfEmpty<T>(commandText);
}

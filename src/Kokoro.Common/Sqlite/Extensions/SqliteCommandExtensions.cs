namespace Kokoro.Common.Sqlite.Extensions;
using Microsoft.Data.Sqlite;

internal static class SqliteCommandExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand In(this SqliteCommand command, SqliteTransaction? transaction) {
		command.Transaction = transaction;
		return command;
	}

	public static SqliteCommand Set(this SqliteCommand command, params SqliteParameter[] parameters) {
		var paramsCol = command.Parameters;
		if (paramsCol.Count > 0) {
			paramsCol.Clear();
		}
		paramsCol.AddRange(parameters);
		return command;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int ExecuteNonQuery(this SqliteCommand command, params SqliteParameter[] parameters)
		=> command.Set(parameters).ExecuteNonQuery();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ExecuteScalar<T>(this SqliteCommand command)
		=> (T)command.ExecuteScalar()!;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ExecuteScalar<T>(this SqliteCommand command, params SqliteParameter[] parameters)
		=> (T)command.Set(parameters).ExecuteScalar()!;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static object? ExecuteScalar(this SqliteCommand command, params SqliteParameter[] parameters)
		=> command.Set(parameters).ExecuteScalar();

	// --

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Exec(this SqliteCommand command)
		=> command.ExecuteNonQuery();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Exec(this SqliteCommand command, params SqliteParameter[] parameters)
		=> command.ExecuteNonQuery(parameters);


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ExecScalar<T>(this SqliteCommand command)
		=> command.ExecuteScalar<T>();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ExecScalar<T>(this SqliteCommand command, params SqliteParameter[] parameters)
		=> command.ExecuteScalar<T>(parameters)!;


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static object? ExecScalar(this SqliteCommand command)
		=> command.ExecuteScalar();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static object? ExecScalar(this SqliteCommand command, params SqliteParameter[] parameters)
		=> command.ExecuteScalar(parameters);
}

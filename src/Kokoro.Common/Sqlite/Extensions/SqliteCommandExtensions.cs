namespace Kokoro.Common.Sqlite.Extensions;
using Microsoft.Data.Sqlite;

internal static class SqliteCommandExtensions {

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand In(this SqliteCommand command, SqliteTransaction? transaction) {
		command.Transaction = transaction;
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand Set(this SqliteCommand command, string commandText) {
		command.CommandText = commandText;
		return command;
	}

	/// <summary>
	/// Same as <see cref="Set(SqliteCommand, string)"/> but always discards
	/// prepared statements (by avoiding the costly <see cref="string"/>
	/// comparison needed to prevent prepared statements from being discarded).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand Reset(this SqliteCommand command, string commandText) {
		command.CommandText = null; // Avoids the cost of a string comparison
		command.CommandText = commandText; // No string comparison will happen here
		return command;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand ClearParams(this SqliteCommand command) {
		command.Parameters.Clear();
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection ClearedParams(this SqliteCommand command) {
		var cmdParams = command.Parameters;
		cmdParams.Clear();
		return cmdParams;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteParameterCollection Params(this SqliteCommand command) {
		return command.Parameters;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1) {
		command.Parameters.Add(param1);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		cmdParams.Add(param3);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		cmdParams.Add(param3);
		cmdParams.Add(param4);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		cmdParams.Add(param3);
		cmdParams.Add(param4);
		cmdParams.Add(param5);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		cmdParams.Add(param3);
		cmdParams.Add(param4);
		cmdParams.Add(param5);
		cmdParams.Add(param6);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		cmdParams.Add(param3);
		cmdParams.Add(param4);
		cmdParams.Add(param5);
		cmdParams.Add(param6);
		cmdParams.Add(param7);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		cmdParams.Add(param3);
		cmdParams.Add(param4);
		cmdParams.Add(param5);
		cmdParams.Add(param6);
		cmdParams.Add(param7);
		cmdParams.Add(param8);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		cmdParams.Add(param3);
		cmdParams.Add(param4);
		cmdParams.Add(param5);
		cmdParams.Add(param6);
		cmdParams.Add(param7);
		cmdParams.Add(param8);
		cmdParams.Add(param9);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9, SqliteParameter param10) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		cmdParams.Add(param3);
		cmdParams.Add(param4);
		cmdParams.Add(param5);
		cmdParams.Add(param6);
		cmdParams.Add(param7);
		cmdParams.Add(param8);
		cmdParams.Add(param9);
		cmdParams.Add(param10);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9, SqliteParameter param10, SqliteParameter param11) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		cmdParams.Add(param3);
		cmdParams.Add(param4);
		cmdParams.Add(param5);
		cmdParams.Add(param6);
		cmdParams.Add(param7);
		cmdParams.Add(param8);
		cmdParams.Add(param9);
		cmdParams.Add(param10);
		cmdParams.Add(param11);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9, SqliteParameter param10, SqliteParameter param11, SqliteParameter param12) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		cmdParams.Add(param3);
		cmdParams.Add(param4);
		cmdParams.Add(param5);
		cmdParams.Add(param6);
		cmdParams.Add(param7);
		cmdParams.Add(param8);
		cmdParams.Add(param9);
		cmdParams.Add(param10);
		cmdParams.Add(param11);
		cmdParams.Add(param12);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9, SqliteParameter param10, SqliteParameter param11, SqliteParameter param12, SqliteParameter param13) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		cmdParams.Add(param3);
		cmdParams.Add(param4);
		cmdParams.Add(param5);
		cmdParams.Add(param6);
		cmdParams.Add(param7);
		cmdParams.Add(param8);
		cmdParams.Add(param9);
		cmdParams.Add(param10);
		cmdParams.Add(param11);
		cmdParams.Add(param12);
		cmdParams.Add(param13);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9, SqliteParameter param10, SqliteParameter param11, SqliteParameter param12, SqliteParameter param13, SqliteParameter param14) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		cmdParams.Add(param3);
		cmdParams.Add(param4);
		cmdParams.Add(param5);
		cmdParams.Add(param6);
		cmdParams.Add(param7);
		cmdParams.Add(param8);
		cmdParams.Add(param9);
		cmdParams.Add(param10);
		cmdParams.Add(param11);
		cmdParams.Add(param12);
		cmdParams.Add(param13);
		cmdParams.Add(param14);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParams(this SqliteCommand command, SqliteParameter param1, SqliteParameter param2, SqliteParameter param3, SqliteParameter param4, SqliteParameter param5, SqliteParameter param6, SqliteParameter param7, SqliteParameter param8, SqliteParameter param9, SqliteParameter param10, SqliteParameter param11, SqliteParameter param12, SqliteParameter param13, SqliteParameter param14, SqliteParameter param15) {
		var cmdParams = command.Parameters;
		cmdParams.Add(param1);
		cmdParams.Add(param2);
		cmdParams.Add(param3);
		cmdParams.Add(param4);
		cmdParams.Add(param5);
		cmdParams.Add(param6);
		cmdParams.Add(param7);
		cmdParams.Add(param8);
		cmdParams.Add(param9);
		cmdParams.Add(param10);
		cmdParams.Add(param11);
		cmdParams.Add(param12);
		cmdParams.Add(param13);
		cmdParams.Add(param14);
		cmdParams.Add(param15);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParamsArray(this SqliteCommand command, SqliteParameter[] parameters) {
		command.Parameters.AddRange(parameters);
		return command;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SqliteCommand AddParamsRange(this SqliteCommand command, IEnumerable<SqliteParameter> parameters) {
		command.Parameters.AddRange(parameters);
		return command;
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int ExecuteNonQuery(this SqliteCommand command)
		=> command.ExecuteNonQuery();

	/// <summary>
	/// Alias for <see cref="Consume(SqliteCommand)"/>
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int ExecuteNonQueryAndDispose(this SqliteCommand command)
		=> command.Consume();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ExecuteScalar<T>(this SqliteCommand command)
		=> (T)command.ExecuteScalar()!;

	/// <summary>
	/// Alias for <see cref="ConsumeScalar{T}(SqliteCommand)"/>
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ExecuteScalarAndDispose<T>(this SqliteCommand command)
		=> command.ConsumeScalar<T>();


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Exec(this SqliteCommand command)
		=> command.ExecuteNonQuery();

	/// <summary>
	/// Alias for <see cref="Consume(SqliteCommand)"/>
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int ExecAndDispose(this SqliteCommand command)
		=> command.Consume();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Consume(this SqliteCommand command) {
		using (command) {
			return command.ExecuteNonQuery();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ExecScalar<T>(this SqliteCommand command)
		=> (T)command.ExecuteScalar()!;

	/// <summary>
	/// Alias for <see cref="ConsumeScalar{T}(SqliteCommand)"/>
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ExecScalarAndDispose<T>(this SqliteCommand command)
		=> command.ConsumeScalar<T>();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ConsumeScalar<T>(this SqliteCommand command) {
		using (command) {
			return (T)command.ExecuteScalar()!;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static object? ExecScalar(this SqliteCommand command)
		=> command.ExecuteScalar();

	/// <summary>
	/// Alias for <see cref="ConsumeScalar(SqliteCommand)"/>
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static object? ExecScalarAndDispose(this SqliteCommand command)
		=> command.ConsumeScalar();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static object? ConsumeScalar(this SqliteCommand command) {
		using (command) {
			return command.ExecuteScalar();
		}
	}
}

namespace Kokoro.Test.Util;
using System.Runtime.Serialization;

/// <summary>
/// An <see cref="AggregateException"/> with very minimal string representation
/// (by not repeating <see cref="AggregateException.InnerExceptions">InnerExceptions</see>'
/// messages).
/// </summary>
[Serializable]
public class MinimalAggregateException : AggregateException, ISerializable {

	public MinimalAggregateException()
		: base("") { }

	public MinimalAggregateException(IEnumerable<Exception> innerExceptions)
		: base("", innerExceptions) { }

	public MinimalAggregateException(params Exception[] innerExceptions)
		: base("", innerExceptions) { }

	public MinimalAggregateException(Exception innerException)
		: base("", innerException) { }

	// --

	public MinimalAggregateException(string? message)
		: base(message ?? "") => _Message = message;

	public MinimalAggregateException(string? message, IEnumerable<Exception> innerExceptions)
		: base(message ?? "", innerExceptions) => _Message = message;

	public MinimalAggregateException(string? message, params Exception[] innerExceptions)
		: base(message ?? "", innerExceptions) => _Message = message;

	public MinimalAggregateException(string? message, Exception innerException)
		: base(message ?? "", innerException) => _Message = message;

	// --

	protected MinimalAggregateException(SerializationInfo info, StreamingContext context)
		: base(info, context) => _Message = info.GetString("Message");

	// --

	private readonly string? _Message;

	public override string Message => _Message ?? "";
}

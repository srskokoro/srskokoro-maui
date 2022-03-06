namespace Kokoro.Test.Util;
using System.Runtime.Serialization;

/// <summary>
/// An <see cref="AggregateException"/> with very minimal string representation
/// (by not repeating <see cref="AggregateException.InnerExceptions">InnerExceptions</see>'
/// messages).
/// </summary>
[Serializable]
public class MiniAggregateException : AggregateException, ISerializable {

	public MiniAggregateException()
		: base("") { }

	public MiniAggregateException(IEnumerable<Exception> innerExceptions)
		: base("", innerExceptions) { }

	public MiniAggregateException(params Exception[] innerExceptions)
		: base("", innerExceptions) { }

	public MiniAggregateException(Exception innerException)
		: base("", innerException) { }

	// --

	public MiniAggregateException(string? message)
		: base(message ?? "") => _Message = message;

	public MiniAggregateException(string? message, IEnumerable<Exception> innerExceptions)
		: base(message ?? "", innerExceptions) => _Message = message;

	public MiniAggregateException(string? message, params Exception[] innerExceptions)
		: base(message ?? "", innerExceptions) => _Message = message;

	public MiniAggregateException(string? message, Exception innerException)
		: base(message ?? "", innerException) => _Message = message;

	// --

	protected MiniAggregateException(SerializationInfo info, StreamingContext context)
		: base(info, context) => _Message = info.GetString("Message");

	// --

	private readonly string? _Message;

	public override string Message => _Message ?? "";
}

namespace Kokoro.Test.Util;
using System.Runtime.Serialization;

public class MinimalAggregateException : AggregateException {

	public MinimalAggregateException() : base("") { }

	public MinimalAggregateException(IEnumerable<Exception> innerExceptions) : base("", innerExceptions) { }

	public MinimalAggregateException(params Exception[] innerExceptions) : base("", innerExceptions) { }

	public MinimalAggregateException(Exception innerException) : base("", innerException) { }

	protected MinimalAggregateException(SerializationInfo info, StreamingContext context) : base(info, context) { }

	public override string Message => "";
}

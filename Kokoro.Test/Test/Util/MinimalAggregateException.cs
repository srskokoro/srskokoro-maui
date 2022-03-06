namespace Kokoro.Test.Util;
using System.Runtime.Serialization;

[Serializable]
public class MinimalAggregateException : AggregateException, ISerializable {

	public MinimalAggregateException() : base("") { }

	public MinimalAggregateException(IEnumerable<Exception> innerExceptions) : base("", innerExceptions) { }

	public MinimalAggregateException(params Exception[] innerExceptions) : base("", innerExceptions) { }

	public MinimalAggregateException(Exception innerException) : base("", innerException) { }

	protected MinimalAggregateException(SerializationInfo info, StreamingContext context) : base(info, context) { }

	public override string Message => "";
}

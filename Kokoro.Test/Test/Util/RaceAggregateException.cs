using System.Runtime.Serialization;

namespace Kokoro.Test.Util;

public class RaceAggregateException : AggregateException {

	public RaceAggregateException() { }

	public RaceAggregateException(IEnumerable<Exception> innerExceptions) : base(null, innerExceptions) { }

	public RaceAggregateException(params Exception[] innerExceptions) : base(null, innerExceptions) { }

	public RaceAggregateException(Exception innerException) : base((string?)null, innerException) { }

	protected RaceAggregateException(SerializationInfo info, StreamingContext context) : base(info, context) { }

	public override string Message => "";
}

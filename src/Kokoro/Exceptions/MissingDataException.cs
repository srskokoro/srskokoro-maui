namespace Kokoro.Exceptions;
using System;
using System.Runtime.Serialization;

[Serializable]
public class MissingDataException : InvalidOperationException {
	private const string DefaultMessage = "Needed data missing";

	public MissingDataException() : base(DefaultMessage) { }

	public MissingDataException(string? message) : base(message) { }

	public MissingDataException(string? message, Exception? innerException) : base(message, innerException) { }

	public MissingDataException(Exception? innerException) : base(DefaultMessage, innerException) { }

	protected MissingDataException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

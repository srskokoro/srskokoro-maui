namespace Kokoro.Exceptions;
using System;
using System.Runtime.Serialization;

[Serializable]
public class ReadOnlyException : UnauthorizedAccessException {
	private const string DefaultMessage = "Read-only";

	public ReadOnlyException() : base(DefaultMessage) { }

	public ReadOnlyException(string? message) : base(message) { }

	public ReadOnlyException(string? message, Exception? innerException) : base(message, innerException) { }

	public ReadOnlyException(Exception? innerException) : base(DefaultMessage, innerException) { }

	protected ReadOnlyException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

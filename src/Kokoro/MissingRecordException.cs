namespace Kokoro;
using System;
using System.Runtime.Serialization;

[Serializable]
public class MissingRecordException : MissingDataException {
	private const string DefaultMessage = "Needed record missing";

	public MissingRecordException() : base(DefaultMessage) { }

	public MissingRecordException(string? message) : base(message) { }

	public MissingRecordException(string? message, Exception? innerException) : base(message, innerException) { }

	public MissingRecordException(Exception? innerException) : base(DefaultMessage, innerException) { }

	protected MissingRecordException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

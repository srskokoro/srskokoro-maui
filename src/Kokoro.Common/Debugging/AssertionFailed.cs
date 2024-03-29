﻿namespace Kokoro.Common.Debugging;
using System;
using System.Runtime.Serialization;

[Obsolete($"Better use `Trace.Fail()` or `Debug.Fail()` instead")]
[Serializable]
internal sealed class AssertionFailed : InvalidOperationException, ISerializable {

	public AssertionFailed() : base("") /* The identifier name says it alreadly */ { }

	public AssertionFailed(string? message) : base(message) { }

	public AssertionFailed(string? message, Exception? innerException) : base(message, innerException) { }

	// CA2229 -- https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2229
	private AssertionFailed(SerializationInfo info, StreamingContext context) : base(info, context) { }
}

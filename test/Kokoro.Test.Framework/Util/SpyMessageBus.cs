namespace Kokoro.Test.Framework.Util;
using System;
using System.Collections.Generic;

// Modified from, https://github.com/xunit/xunit/blob/2.4.1/test/test.utility/TestDoubles/SpyMessageBus.cs
public class SpyMessageBus : LongLivedMarshalByRefObject, Xunit.Sdk.IMessageBus {
	private readonly Func<IMessageSinkMessage, bool> _CancellationThunk;
	private List<IMessageSinkMessage>? _Messages = new();

	public SpyMessageBus(Func<IMessageSinkMessage, bool>? cancellationThunk = null) {
		_CancellationThunk = cancellationThunk ?? (msg => true);
	}

	public List<IMessageSinkMessage> Messages => _Messages
		?? throw new ObjectDisposedException(nameof(SpyMessageBus));

	/// <inheritdoc/>
	public virtual void Dispose() {
		var msgs = _Messages;
		if (msgs == null) {
			return; // Already disposed
		}

		foreach (var msg in msgs) {
			if (msg is IDisposable disposable) {
				disposable.Dispose();
			}
		}

		_Messages = null; // Mark as disposed
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc/>
	public bool QueueMessage(IMessageSinkMessage message) {
		Messages.Add(message);
		return _CancellationThunk(message);
	}
}

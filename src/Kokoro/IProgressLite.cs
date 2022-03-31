namespace Kokoro;

/// <summary>
/// A common interface for implementations of lightweight progress reporting
/// that doesn't actually report progress. A UI should instead periodically
/// check the structure for changes in progress.
/// </summary>
/// <seealso cref="IProgress{T}"/>
public interface IProgressLite { }

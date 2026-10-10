namespace Jev;

/// <summary>
/// A jev-eval <c>thresholds.json</c> could not be used: it is malformed, has an unsupported
/// version, or holds no gate that fits the questions you asked.
/// </summary>
public sealed class JevThresholdsException(string message, Exception? inner = null)
    : JevException(message, inner: inner);

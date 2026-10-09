namespace Jev;

/// <summary>
/// The answer to a <see cref="Noul"/>. It carries no separate confidence: gate on the
/// probability itself, where near 0.5 means genuinely uncertain rather than "medium yes".
/// </summary>
/// <param name="Probability">Probability in [0, 1] that the condition holds.</param>
public sealed record NoulAnswer(double Probability) : Answer
{
    /// <summary>
    /// True when <see cref="Probability"/> is at or above <paramref name="threshold"/>, the
    /// same rule <see cref="CalibratedAnswer.IsConfident"/> uses.
    /// </summary>
    public bool IsTrue(double threshold) => Probability >= threshold;
}

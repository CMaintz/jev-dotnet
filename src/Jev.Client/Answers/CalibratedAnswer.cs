namespace Jev;

/// <summary>
/// An answer drawn from a probability distribution over options or levels, so it
/// carries a <see cref="Confidence"/> you can gate on. A <see cref="NoulAnswer"/> is not
/// calibrated this way; gate it on its probability instead.
/// </summary>
public abstract record CalibratedAnswer : Answer
{
    private protected CalibratedAnswer(double? confidence)
    {
        Confidence = confidence;
    }

    /// <summary>How peaked the distribution is, in [0, 1]; null if the service omitted it.</summary>
    public double? Confidence { get; }

    /// <summary>
    /// True when <see cref="Confidence"/> is at or above <paramref name="threshold"/>;
    /// false when the confidence is missing.
    /// </summary>
    public bool IsConfident(double threshold) => Confidence is { } c && c >= threshold;
}

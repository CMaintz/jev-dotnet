namespace Jev;

/// <summary>
/// One typed judgment returned by Jev, keyed by the question id you sent. Which
/// fields are populated depends on <see cref="Type"/>: a Noul fills only
/// <see cref="NoulValue"/>; a Choice fills <see cref="ChoiceValue"/>,
/// <see cref="Probabilities"/>, and <see cref="Confidence"/>; a Score fills
/// <see cref="ScoreValue"/>, <see cref="Legend"/>, <see cref="ScoreProbabilities"/>,
/// and <see cref="Confidence"/>.
/// </summary>
public sealed record Answer
{
    /// <summary>Wire discriminator: <c>noul</c>, <c>choice</c>, or <c>score</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Noul probability in [0, 1]; null unless <see cref="Type"/> is <c>noul</c>.</summary>
    public double? NoulValue { get; init; }

    /// <summary>Selected option; null unless <see cref="Type"/> is <c>choice</c>.</summary>
    public string? ChoiceValue { get; init; }

    /// <summary>Per-option probabilities for a Choice; null otherwise.</summary>
    public IReadOnlyDictionary<string, double>? Probabilities { get; init; }

    /// <summary>Numeric position on the scale for a Score; null otherwise. May fall between levels.</summary>
    public double? ScoreValue { get; init; }

    /// <summary>Per-level probabilities for a Score, ordered low to high; null otherwise.</summary>
    public IReadOnlyList<double>? ScoreProbabilities { get; init; }

    /// <summary>Index-to-description level map for a Score; null otherwise.</summary>
    public IReadOnlyDictionary<string, string>? Legend { get; init; }

    /// <summary>Distribution peakedness in [0, 1] for Choice and Score; null for Noul.</summary>
    public double? Confidence { get; init; }

    /// <summary>
    /// True when this answer carries a <see cref="Confidence"/> at or above <paramref name="threshold"/>.
    /// A Noul (no confidence) is never "confident" by this test; gate it on <see cref="NoulValue"/> instead.
    /// </summary>
    public bool IsConfident(double threshold) => Confidence is { } c && c >= threshold;
}

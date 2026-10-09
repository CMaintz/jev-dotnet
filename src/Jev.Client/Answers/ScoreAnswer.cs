using System.Collections.ObjectModel;

namespace Jev;

/// <summary>The answer to a <see cref="Jev.Score"/>.</summary>
public sealed record ScoreAnswer : CalibratedAnswer
{
    /// <param name="score">Position on the scale; may fall between two levels.</param>
    /// <param name="probabilities">Probability per level, ordered low to high.</param>
    /// <param name="legend">Level index (as text, e.g. <c>"0"</c>) to its description.</param>
    /// <param name="confidence">Distribution peakedness in [0, 1], or null if omitted.</param>
    public ScoreAnswer(
        double score,
        IReadOnlyList<double> probabilities,
        IReadOnlyDictionary<string, string> legend,
        double? confidence)
        : base(confidence)
    {
        ArgumentNullException.ThrowIfNull(probabilities);
        ArgumentNullException.ThrowIfNull(legend);
        Score = score;
        Probabilities = Array.AsReadOnly(probabilities.ToArray());
        Legend = new ReadOnlyDictionary<string, string>(legend.ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    /// <summary>Position on the scale; may fall between two levels.</summary>
    public double Score { get; }

    /// <summary>Probability per level, ordered low to high.</summary>
    public IReadOnlyList<double> Probabilities { get; }

    /// <summary>Level index (as text) to its description.</summary>
    public IReadOnlyDictionary<string, string> Legend { get; }

    /// <inheritdoc />
    public bool Equals(ScoreAnswer? other) =>
        other is not null && base.Equals(other)
        && Score.Equals(other.Score)
        && ContentEquality.SameItems(Probabilities, other.Probabilities)
        && ContentEquality.SameEntries(Legend, other.Legend);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Score, Probabilities.Count);
}

namespace Jev;

/// <summary>
/// Rate the state along an ordered scale you describe. Levels are ordered low to
/// high; each must describe a concrete situation and stand on its own. The answer's
/// score can fall between two levels, with a probability across levels and a
/// confidence. Use comparable per-item Scores for graded ranking.
/// </summary>
public sealed record Score : Question
{
    /// <summary>The fewest levels a Score may have.</summary>
    public const int MinLevels = 2;

    /// <summary>The most levels a Score may have.</summary>
    public const int MaxLevels = 10;

    /// <param name="instructions">The dimension to rate, e.g. "How frustrated the customer appears".</param>
    /// <param name="criteria">Ordered level descriptions, low to high. Between 2 and 10 levels.</param>
    public Score(string instructions, IReadOnlyList<string> criteria)
        : base(instructions)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count is < MinLevels or > MaxLevels)
        {
            throw new ArgumentException(
                $"A Score needs between {MinLevels} and {MaxLevels} ordered levels.", nameof(criteria));
        }

        for (var i = 0; i < criteria.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(criteria[i]))
            {
                throw new ArgumentException($"Level {i} needs a non-blank description.", nameof(criteria));
            }
        }

        Criteria = Array.AsReadOnly(criteria.ToArray());
    }

    /// <summary>Ordered level descriptions, low to high. Sent as the wire <c>criteria</c> array.</summary>
    public IReadOnlyList<string> Criteria { get; }

    /// <inheritdoc />
    public bool Equals(Score? other) =>
        other is not null && base.Equals(other) && ContentEquality.SameItems(Criteria, other.Criteria);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Criteria.Count);
}

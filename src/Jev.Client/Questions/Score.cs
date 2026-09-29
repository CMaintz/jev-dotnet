namespace Jev;

/// <summary>
/// Rate the state along an ordered scale you describe. Levels are ordered low to
/// high; each must describe a concrete situation and stand on its own. The answer's
/// score can fall between two levels, with a probability across levels and a
/// confidence. Use comparable per-item Scores for graded ranking.
/// </summary>
public sealed record Score : Question
{
    /// <param name="instructions">The dimension to rate, e.g. "How frustrated the customer appears".</param>
    /// <param name="criteria">Ordered level descriptions, low to high. Between 2 and 10 levels.</param>
    public Score(string instructions, IReadOnlyList<string> criteria)
        : base("score", instructions)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count is < 2 or > 10)
        {
            throw new ArgumentException("A Score needs between 2 and 10 ordered levels.", nameof(criteria));
        }

        Criteria = criteria;
    }

    /// <summary>Ordered level descriptions, low to high. Sent verbatim as the wire <c>criteria</c> array.</summary>
    public IReadOnlyList<string> Criteria { get; }
}

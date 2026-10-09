namespace Jev;

/// <summary>
/// A yes/no judgment. The answer is a single probability in [0, 1]: near 1 is a
/// strong yes, near 0 a strong no, near 0.5 genuinely uncertain. A Noul carries no
/// separate confidence. Use one Noul per label when several labels may apply at once.
/// </summary>
public sealed record Noul : Question
{
    /// <param name="instructions">The condition to judge, e.g. "Does the customer request a refund?".</param>
    /// <param name="whenTrue">Optional description of what a "yes" means (wire <c>criteria.true</c>).</param>
    /// <param name="whenFalse">Optional description of what a "no" means (wire <c>criteria.false</c>).</param>
    public Noul(string instructions, string? whenTrue = null, string? whenFalse = null)
        : base(instructions)
    {
        WhenTrue = whenTrue;
        WhenFalse = whenFalse;
    }

    /// <summary>Optional gloss for the positive case.</summary>
    public string? WhenTrue { get; }

    /// <summary>Optional gloss for the negative case.</summary>
    public string? WhenFalse { get; }
}

using System.Text.Json;

namespace Jev;

/// <summary>
/// A yes/no judgment. The answer is a single probability in [0, 1]: near 1 is a
/// strong yes, near 0 a strong no, near 0.5 genuinely uncertain. A Noul carries no
/// separate confidence. Use one Noul per label when several labels may apply at once.
/// </summary>
public sealed record Noul : Question
{
    /// <param name="instructions">The condition to judge, e.g. "Does the customer request a refund?"; text or structured JSON.</param>
    /// <param name="whenTrue">Optional description of what a "yes" means (wire <c>criteria.true</c>); text or structured JSON.</param>
    /// <param name="whenFalse">Optional description of what a "no" means (wire <c>criteria.false</c>); text or structured JSON.</param>
    public Noul(object instructions, object? whenTrue = null, object? whenFalse = null)
        : base(instructions)
    {
        WhenTrue = Prompts.OptionalCriterion(whenTrue, nameof(whenTrue));
        WhenFalse = Prompts.OptionalCriterion(whenFalse, nameof(whenFalse));
    }

    /// <summary>Optional gloss for the positive case.</summary>
    public JsonElement? WhenTrue { get; }

    /// <summary>Optional gloss for the negative case.</summary>
    public JsonElement? WhenFalse { get; }

    /// <inheritdoc />
    public bool Equals(Noul? other) =>
        other is not null && base.Equals(other) && Same(WhenTrue, other.WhenTrue) && Same(WhenFalse, other.WhenFalse);

    /// <inheritdoc />
    public override int GetHashCode() => base.GetHashCode();

    private static bool Same(JsonElement? left, JsonElement? right) =>
        left is { } l ? right is { } r && Prompts.Same(l, r) : right is null;
}

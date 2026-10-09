using System.Collections.ObjectModel;

namespace Jev;

/// <summary>The answer to a <see cref="Jev.Choice"/>.</summary>
public sealed record ChoiceAnswer : CalibratedAnswer
{
    /// <param name="choice">The selected option.</param>
    /// <param name="probabilities">Probability per option.</param>
    /// <param name="confidence">Distribution peakedness in [0, 1], or null if omitted.</param>
    public ChoiceAnswer(string choice, IReadOnlyDictionary<string, double> probabilities, double? confidence)
        : base(confidence)
    {
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(probabilities);
        Choice = choice;
        Probabilities = new ReadOnlyDictionary<string, double>(probabilities.ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    /// <summary>The selected option.</summary>
    public string Choice { get; }

    /// <summary>Probability per option.</summary>
    public IReadOnlyDictionary<string, double> Probabilities { get; }

    /// <inheritdoc />
    public bool Equals(ChoiceAnswer? other) =>
        other is not null && base.Equals(other) && Choice == other.Choice && ContentEquality.SameEntries(Probabilities, other.Probabilities);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Choice, Probabilities.Count);
}

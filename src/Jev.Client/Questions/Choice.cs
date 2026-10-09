using System.Collections.ObjectModel;

namespace Jev;

/// <summary>
/// Pick exactly one option from a defined set. The answer names the selected option
/// and gives a probability for every option; its confidence summarizes how peaked
/// that distribution is. Add a no-match option when nothing may fit.
/// </summary>
public sealed record Choice : Question
{
    /// <summary>The most options a single Choice may offer.</summary>
    public const int MaxOptions = 255;

    /// <param name="instructions">What to decide, e.g. "Which team should handle this".</param>
    /// <param name="criteria">
    /// Option name to its description; 1 to 255 options. The model cannot pick an option
    /// you omit.
    /// </param>
    public Choice(string instructions, IReadOnlyDictionary<string, string> criteria)
        : base(instructions)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count is 0 or > MaxOptions)
        {
            throw new ArgumentException($"A Choice needs between 1 and {MaxOptions} options.", nameof(criteria));
        }

        foreach (var (option, description) in criteria)
        {
            if (string.IsNullOrWhiteSpace(option) || string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException($"Option '{option}' needs a non-blank name and description.", nameof(criteria));
            }
        }

        Criteria = new ReadOnlyDictionary<string, string>(criteria.ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    /// <summary>Option name to description. Sent as the wire <c>criteria</c> map.</summary>
    public IReadOnlyDictionary<string, string> Criteria { get; }

    /// <inheritdoc />
    public bool Equals(Choice? other) =>
        other is not null && base.Equals(other) && ContentEquality.SameEntries(Criteria, other.Criteria);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Criteria.Count);
}

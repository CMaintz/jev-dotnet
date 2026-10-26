using System.Collections.ObjectModel;
using System.Text.Json;

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

    /// <param name="instructions">What to decide, e.g. "Which team should handle this"; text or structured JSON.</param>
    /// <param name="criteria">
    /// Option name to its description; 1 to 255 options. The model cannot pick an option
    /// you omit.
    /// </param>
    public Choice(object instructions, IReadOnlyDictionary<string, string> criteria)
        : this(instructions, Widen(criteria))
    {
    }

    /// <param name="instructions">What to decide; text or structured JSON.</param>
    /// <param name="criteria">Option name to its description, each text or structured JSON; 1 to 255 options.</param>
    public Choice(object instructions, IReadOnlyDictionary<string, object> criteria)
        : base(instructions)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count is 0 or > MaxOptions)
        {
            throw new ArgumentException($"A Choice needs between 1 and {MaxOptions} options.", nameof(criteria));
        }

        var options = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (option, description) in criteria)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(option, nameof(criteria));
            options[option] = Prompts.Criterion(description, $"description of option '{option}'", Images);
        }

        Criteria = new ReadOnlyDictionary<string, JsonElement>(options);
    }

    /// <summary>Option name to description, in the order given. Sent as the wire <c>criteria</c> map.</summary>
    public IReadOnlyDictionary<string, JsonElement> Criteria { get; }

    /// <inheritdoc />
    public bool Equals(Choice? other) =>
        other is not null && base.Equals(other) && Criteria.Count == other.Criteria.Count
        && Criteria.All(kv => other.Criteria.TryGetValue(kv.Key, out var description) && Prompts.Same(kv.Value, description));

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Criteria.Count);

    private static Dictionary<string, object> Widen(IReadOnlyDictionary<string, string> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return criteria.ToDictionary(kv => kv.Key, kv => (object)kv.Value, StringComparer.Ordinal);
    }
}

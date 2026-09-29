namespace Jev;

/// <summary>
/// Pick exactly one option from a defined set. The answer names the selected option
/// and gives a probability for every option; its confidence summarizes how peaked
/// that distribution is. Add a no-match option when nothing may fit.
/// </summary>
public sealed record Choice : Question
{
    /// <param name="instructions">What to decide, e.g. "Which team should handle this".</param>
    /// <param name="criteria">Option name to its description. Max 255 options; the model cannot pick an option you omit.</param>
    public Choice(string instructions, IReadOnlyDictionary<string, string> criteria)
        : base("choice", instructions)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Count == 0)
        {
            throw new ArgumentException("A Choice needs at least one option.", nameof(criteria));
        }

        Criteria = criteria;
    }

    /// <summary>Option name to description. Sent verbatim as the wire <c>criteria</c> map.</summary>
    public IReadOnlyDictionary<string, string> Criteria { get; }
}

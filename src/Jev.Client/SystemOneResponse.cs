namespace Jev;

/// <summary>
/// The result of one <c>system_one</c> request: every answer keyed by the question
/// id you sent, plus token usage. Use the indexer for a known question, or the
/// type-filtered views (<see cref="Choices"/>, <see cref="Scores"/>, <see cref="Nouls"/>)
/// to iterate one kind.
/// </summary>
public sealed record SystemOneResponse
{
    /// <summary>The model that answered (e.g. the resolved version behind <c>jev-latest</c>).</summary>
    public required string Model { get; init; }

    /// <summary>Answers keyed by question id.</summary>
    public required IReadOnlyDictionary<string, Answer> Answers { get; init; }

    /// <summary>Token accounting for the request.</summary>
    public Usage? Usage { get; init; }

    /// <summary>The answer for <paramref name="questionId"/>.</summary>
    public Answer this[string questionId] => Answers[questionId];

    /// <summary>Answers whose type is <c>choice</c>, keyed by question id.</summary>
    public IReadOnlyDictionary<string, Answer> Choices => Filter("choice");

    /// <summary>Answers whose type is <c>score</c>, keyed by question id.</summary>
    public IReadOnlyDictionary<string, Answer> Scores => Filter("score");

    /// <summary>Answers whose type is <c>noul</c>, keyed by question id.</summary>
    public IReadOnlyDictionary<string, Answer> Nouls => Filter("noul");

    private Dictionary<string, Answer> Filter(string type) =>
        Answers.Where(kv => kv.Value.Type == type).ToDictionary(kv => kv.Key, kv => kv.Value);
}

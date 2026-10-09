using System.Collections.ObjectModel;

namespace Jev;

/// <summary>
/// The result of one <c>system_one</c> request: every answer keyed by the question id
/// you sent, plus token usage. Use the typed accessors (<see cref="GetChoice"/>,
/// <see cref="GetScore"/>, <see cref="GetNoul"/>) for a known question, the indexer for
/// any answer, or the type-filtered views (<see cref="Choices"/>, <see cref="Scores"/>,
/// <see cref="Nouls"/>) to iterate one kind.
/// </summary>
public sealed record SystemOneResponse
{
    /// <summary>The model that answered (e.g. the resolved version behind <c>jev-latest</c>), or null if omitted.</summary>
    public string? Model { get; init; }

    private readonly IReadOnlyDictionary<string, Answer> _answers = ReadOnlyDictionary<string, Answer>.Empty;

    /// <summary>Answers keyed by question id; a read-only copy of what was supplied.</summary>
    public required IReadOnlyDictionary<string, Answer> Answers
    {
        get => _answers;
        init => _answers = new ReadOnlyDictionary<string, Answer>(value.ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    /// <summary>Token accounting for the request, or null if the service omitted it.</summary>
    public Usage? Usage { get; init; }

    /// <summary>The answer for <paramref name="questionId"/>, whatever its type.</summary>
    /// <exception cref="KeyNotFoundException">No answer was returned under that id.</exception>
    public Answer this[string questionId] =>
        Answers.TryGetValue(questionId, out var answer)
            ? answer
            : throw new KeyNotFoundException($"No answer for question id: {questionId}");

    /// <summary>The answer to the <see cref="Choice"/> asked under <paramref name="questionId"/>.</summary>
    /// <exception cref="KeyNotFoundException">No answer was returned under that id.</exception>
    /// <exception cref="InvalidOperationException">That question was not a Choice.</exception>
    public ChoiceAnswer GetChoice(string questionId) => Get<ChoiceAnswer>(questionId);

    /// <summary>The answer to the <see cref="Score"/> asked under <paramref name="questionId"/>; throws like <see cref="GetChoice"/>.</summary>
    public ScoreAnswer GetScore(string questionId) => Get<ScoreAnswer>(questionId);

    /// <summary>The answer to the <see cref="Noul"/> asked under <paramref name="questionId"/>; throws like <see cref="GetChoice"/>.</summary>
    public NoulAnswer GetNoul(string questionId) => Get<NoulAnswer>(questionId);

    /// <summary>Every <see cref="ChoiceAnswer"/>, keyed by question id, as a new dictionary.</summary>
    public IReadOnlyDictionary<string, ChoiceAnswer> Choices() => Filter<ChoiceAnswer>();

    /// <summary>Every <see cref="ScoreAnswer"/>, keyed by question id, as a new dictionary.</summary>
    public IReadOnlyDictionary<string, ScoreAnswer> Scores() => Filter<ScoreAnswer>();

    /// <summary>Every <see cref="NoulAnswer"/>, keyed by question id, as a new dictionary.</summary>
    public IReadOnlyDictionary<string, NoulAnswer> Nouls() => Filter<NoulAnswer>();

    /// <inheritdoc />
    public bool Equals(SystemOneResponse? other) =>
        other is not null
        && Model == other.Model
        && Equals(Usage, other.Usage)
        && ContentEquality.SameEntries(Answers, other.Answers);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Model, Usage, Answers.Count);

    private T Get<T>(string questionId)
        where T : Answer
    {
        var answer = this[questionId];
        return answer as T ?? throw new InvalidOperationException(
            $"Answer '{questionId}' is a {answer.GetType().Name}, not a {typeof(T).Name}.");
    }

    private Dictionary<string, T> Filter<T>()
        where T : Answer =>
        Answers.Where(kv => kv.Value is T).ToDictionary(kv => kv.Key, kv => (T)kv.Value);
}

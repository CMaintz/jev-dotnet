using System.Text.Json;

namespace Jev.Json;

/// <summary>
/// Decodes a <c>system_one</c> response into typed answers. Each answer's shape is
/// taken from the question asked under its id, so the wire <c>type</c> field is never
/// needed. Answers under ids that were not asked are ignored, and an asked id with no
/// answer is simply absent. A body that is not JSON, has no <c>answers</c> object, or has
/// an answer missing its value or carrying a non-finite number surfaces as a
/// <see cref="JevException"/> carrying the raw body.
/// </summary>
internal sealed class ResponseReader
{
    private readonly string _body;

    private ResponseReader(string body) => _body = body;

    public static SystemOneResponse Parse(string body, IReadOnlyDictionary<string, Question> questions) =>
        new ResponseReader(body).Parse(questions);

    private SystemOneResponse Parse(IReadOnlyDictionary<string, Question> questions)
    {
        using var doc = ParseDocument();
        var root = RequireObject(doc.RootElement, "response");
        return new SystemOneResponse
        {
            Model = OptString(root, "model"),
            Answers = ReadAnswers(root, questions),
            Usage = ReadUsage(root),
        };
    }

    private JsonDocument ParseDocument()
    {
        try
        {
            return JsonDocument.Parse(_body);
        }
        catch (JsonException e)
        {
            throw new JevException($"Malformed response: {e.Message}", responseBody: _body, inner: e);
        }
    }

    private Dictionary<string, Answer> ReadAnswers(JsonElement root, IReadOnlyDictionary<string, Question> questions)
    {
        var answers = new Dictionary<string, Answer>();
        var block = root.TryGetProperty("answers", out var found) ? found : default;
        foreach (var entry in RequireObject(block, "answers").EnumerateObject())
        {
            if (questions.TryGetValue(entry.Name, out var question))
            {
                answers[entry.Name] = ReadAnswer(question, RequireObject(entry.Value, $"answer '{entry.Name}'"));
            }
        }

        return answers;
    }

    private Answer ReadAnswer(Question question, JsonElement e) => question switch
    {
        Choice => new ChoiceAnswer(RequireString(e, "choice"), ReadProbabilityMap(e), OptDouble(e, "confidence")),
        Score => new ScoreAnswer(
            RequireDouble(e, "score"), ReadProbabilityList(e), ReadLegend(e), OptDouble(e, "confidence")),
        Noul => new NoulAnswer(RequireDouble(e, "noul")),
        _ => throw new InvalidOperationException($"Unknown question type {question.GetType().Name}."),
    };

    private Dictionary<string, double> ReadProbabilityMap(JsonElement e)
    {
        var map = new Dictionary<string, double>();
        if (e.TryGetProperty("probabilities", out var p) && p.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in p.EnumerateObject())
            {
                map[entry.Name] = Number(entry.Value);
            }
        }

        return map;
    }

    private List<double> ReadProbabilityList(JsonElement e)
    {
        var list = new List<double>();
        if (e.TryGetProperty("probabilities", out var p) && p.ValueKind == JsonValueKind.Array)
        {
            list.AddRange(p.EnumerateArray().Select(Number));
        }

        return list;
    }

    private static Dictionary<string, string> ReadLegend(JsonElement e)
    {
        var map = new Dictionary<string, string>();
        if (e.TryGetProperty("legend", out var m) && m.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in m.EnumerateObject())
            {
                map[entry.Name] = entry.Value.ToString();
            }
        }

        return map;
    }

    private static Usage? ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var u) || u.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new Usage { InputTokens = TokenCount(u, "input_tokens"), OutputTokens = TokenCount(u, "output_tokens") };
    }

    private JsonElement RequireObject(JsonElement e, string what) =>
        e.ValueKind == JsonValueKind.Object ? e : throw Malformed($"{what} is not a JSON object");

    private string RequireString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()!
            : throw Malformed($"missing string field '{name}'");

    private double RequireDouble(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? Number(v)
            : throw Malformed($"missing numeric field '{name}'");

    private double? OptDouble(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? Number(v) : null;

    private double Number(JsonElement v) =>
        v.ValueKind == JsonValueKind.Number && double.IsFinite(v.GetDouble())
            ? v.GetDouble()
            : throw Malformed($"expected a finite number but got {v}");

    private static int TokenCount(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var count) ? count : 0;

    private static string? OptString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private JevException Malformed(string detail) => new($"Malformed response: {detail}.", responseBody: _body);
}

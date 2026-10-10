using System.Text.Json;

namespace Jev.Json;

/// <summary>
/// Decodes a jev-eval <c>thresholds.json</c> (contract version 1). Only the keys the
/// picker needs are read; unknown keys are ignored. Anything malformed surfaces as a
/// <see cref="JevThresholdsException"/> naming <c>source</c> (the file path, when loaded).
/// </summary>
internal sealed class ThresholdsReader
{
    private readonly string _source;

    private ThresholdsReader(string source) => _source = source;

    public static JevThresholds Parse(string json, string source) => new ThresholdsReader(source).Parse(json);

    private JevThresholds Parse(string json)
    {
        using var doc = ParseDocument(json);
        var root = RequireObject(doc.RootElement, "the document");
        RequireVersion(root);
        var unreadable = new HashSet<string>(StringComparer.Ordinal);
        return new JevThresholds(
            RequireString(root, "model"),
            ReadQuestions(root),
            ReadComposite(root),
            ReadDefinitions(root, unreadable),
            unreadable);
    }

    private JsonDocument ParseDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new JevThresholdsException($"{_source}: not valid JSON: {e.Message}", e);
        }
    }

    private void RequireVersion(JsonElement root)
    {
        var found = root.TryGetProperty("version", out var v);
        if (!found || v.ValueKind != JsonValueKind.Number || v.GetDouble() != 1)
        {
            throw Malformed($"expected version 1, got {(found ? v.GetRawText() : "none")}");
        }
    }

    private Dictionary<string, QuestionGate> ReadQuestions(JsonElement root)
    {
        var block = root.TryGetProperty("questions", out var found) ? found : default;
        var gates = new Dictionary<string, QuestionGate>(StringComparer.Ordinal);
        foreach (var entry in RequireObject(block, "\"questions\"").EnumerateObject())
        {
            var e = RequireObject(entry.Value, $"questions entry \"{entry.Name}\"");
            gates[entry.Name] = new QuestionGate(RequireString(e, "type"), ReadGate(e));
        }

        return gates;
    }

    private CompositeGate? ReadComposite(JsonElement root)
    {
        if (!root.TryGetProperty("composite", out var c))
        {
            return null;
        }

        var e = RequireObject(c, "\"composite\"");
        return new CompositeGate(ReadGate(e), ReadIds(e));
    }

    private List<string> ReadIds(JsonElement composite)
    {
        var valid = composite.TryGetProperty("questions", out var q) && q.ValueKind == JsonValueKind.Array
            && q.EnumerateArray().All(id => id.ValueKind == JsonValueKind.String);
        return valid
            ? q.EnumerateArray().Select(id => id.GetString()!).ToList()
            : throw Malformed("\"composite.questions\" is not a list of question ids");
    }

    private static Dictionary<string, Question>? ReadDefinitions(JsonElement root, HashSet<string> unreadable)
    {
        if (!root.TryGetProperty("definitions", out var block) || block.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var definitions = new Dictionary<string, Question>(StringComparer.Ordinal);
        foreach (var entry in block.EnumerateObject())
        {
            if (DefinitionReader.TryRead(entry.Value) is { } question)
            {
                definitions[entry.Name] = question;
            }
            else
            {
                unreadable.Add(entry.Name);
            }
        }

        return definitions;
    }

    private ThresholdGate ReadGate(JsonElement e) =>
        new(RequireDouble(e, "threshold"), RequireDouble(e, "accuracy"), RequireDouble(e, "coverage"), RequireCount(e, "n"));

    private JsonElement RequireObject(JsonElement e, string what) =>
        e.ValueKind == JsonValueKind.Object ? e : throw Malformed($"{what} is missing or not a JSON object");

    private string RequireString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()!
            : throw Malformed($"missing string field \"{name}\"");

    private double RequireDouble(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && double.IsFinite(v.GetDouble())
            ? v.GetDouble()
            : throw Malformed($"missing numeric field \"{name}\"");

    private int RequireCount(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)
            ? n
            : throw Malformed($"missing integer field \"{name}\"");

    private JevThresholdsException Malformed(string detail) => new($"{_source}: {detail}");
}

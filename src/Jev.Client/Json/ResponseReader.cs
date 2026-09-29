using System.Text.Json;

namespace Jev.Json;

/// <summary>
/// Parses a <c>system_one</c> response. Read by hand because <c>probabilities</c> is a
/// map for a Choice but an array for a Score, and the per-answer <c>type</c> field may
/// be absent (then it is inferred from which value fields are present).
/// </summary>
internal static class ResponseReader
{
    public static SystemOneResponse Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new SystemOneResponse
        {
            Model = OptString(root, "model") ?? string.Empty,
            Answers = ReadAnswers(root),
            Usage = ReadUsage(root),
        };
    }

    private static Dictionary<string, Answer> ReadAnswers(JsonElement root)
    {
        var answers = new Dictionary<string, Answer>();
        if (root.TryGetProperty("answers", out var block) && block.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in block.EnumerateObject())
            {
                answers[entry.Name] = ReadAnswer(entry.Value);
            }
        }

        return answers;
    }

    private static Answer ReadAnswer(JsonElement e) => new()
    {
        Type = OptString(e, "type") ?? Infer(e),
        NoulValue = OptDouble(e, "noul"),
        ChoiceValue = OptString(e, "choice"),
        Probabilities = ReadProbabilityMap(e),
        ScoreValue = OptDouble(e, "score"),
        ScoreProbabilities = ReadProbabilityArray(e),
        Legend = ReadStringMap(e, "legend"),
        Confidence = OptDouble(e, "confidence"),
    };

    private static string Infer(JsonElement e)
    {
        if (Has(e, "choice"))
        {
            return "choice";
        }

        if (Has(e, "score"))
        {
            return "score";
        }

        return Has(e, "noul") ? "noul" : string.Empty;
    }

    private static Dictionary<string, double>? ReadProbabilityMap(JsonElement e)
    {
        if (!e.TryGetProperty("probabilities", out var p) || p.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var map = new Dictionary<string, double>();
        foreach (var entry in p.EnumerateObject())
        {
            map[entry.Name] = entry.Value.GetDouble();
        }

        return map;
    }

    private static List<double>? ReadProbabilityArray(JsonElement e)
    {
        if (!e.TryGetProperty("probabilities", out var p) || p.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<double>();
        foreach (var item in p.EnumerateArray())
        {
            list.Add(item.GetDouble());
        }

        return list;
    }

    private static Dictionary<string, string>? ReadStringMap(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var m) || m.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var map = new Dictionary<string, string>();
        foreach (var entry in m.EnumerateObject())
        {
            map[entry.Name] = entry.Value.GetString() ?? string.Empty;
        }

        return map;
    }

    private static Usage? ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var u) || u.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new Usage
        {
            InputTokens = (int)(OptDouble(u, "input_tokens") ?? 0),
            OutputTokens = (int)(OptDouble(u, "output_tokens") ?? 0),
        };
    }

    private static bool Has(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is not JsonValueKind.Null;

    private static string? OptString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? OptDouble(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
}

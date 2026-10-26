using System.Text.Json;

namespace Jev.Json;

/// <summary>
/// Rebuilds a wire question (as jev-eval recorded it under <c>definitions</c>) into a
/// typed <see cref="Question"/>, so it can be compared by value with the one a caller
/// sends. Returns null for anything that is not a valid question.
/// </summary>
internal static class DefinitionReader
{
    public static Question? TryRead(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("instructions", out var instructions))
        {
            return null;
        }

        try
        {
            return OptString(e, "type") switch
            {
                "choice" => ReadChoice(e, instructions),
                "score" => ReadScore(e, instructions),
                "noul" => ReadNoul(e, instructions),
                _ => null,
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static Choice? ReadChoice(JsonElement e, JsonElement instructions)
    {
        if (!e.TryGetProperty("criteria", out var c) || c.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var criteria = c.EnumerateObject().ToDictionary(option => option.Name, option => (object)option.Value, StringComparer.Ordinal);
        return new Choice(instructions, criteria);
    }

    private static Score? ReadScore(JsonElement e, JsonElement instructions) =>
        e.TryGetProperty("criteria", out var c) && c.ValueKind == JsonValueKind.Array
            ? new Score(instructions, c.EnumerateArray().Select(level => (object)level).ToList())
            : null;

    private static Noul ReadNoul(JsonElement e, JsonElement instructions)
    {
        var criteria = e.TryGetProperty("criteria", out var c) && c.ValueKind == JsonValueKind.Object ? c : default;
        return criteria.ValueKind == JsonValueKind.Object
            ? new Noul(instructions, OptValue(criteria, "true"), OptValue(criteria, "false"))
            : new Noul(instructions);
    }

    private static string? OptString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static JsonElement? OptValue(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v : null;
}

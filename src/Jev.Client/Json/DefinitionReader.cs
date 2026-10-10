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
        if (e.ValueKind != JsonValueKind.Object || OptString(e, "instructions") is not { } instructions)
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

    private static Choice? ReadChoice(JsonElement e, string instructions)
    {
        if (!e.TryGetProperty("criteria", out var c) || c.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var criteria = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var option in c.EnumerateObject())
        {
            if (option.Value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            criteria[option.Name] = option.Value.GetString()!;
        }

        return new Choice(instructions, criteria);
    }

    private static Score? ReadScore(JsonElement e, string instructions)
    {
        if (!e.TryGetProperty("criteria", out var c) || c.ValueKind != JsonValueKind.Array
            || c.EnumerateArray().Any(level => level.ValueKind != JsonValueKind.String))
        {
            return null;
        }

        return new Score(instructions, c.EnumerateArray().Select(level => level.GetString()!).ToList());
    }

    private static Noul ReadNoul(JsonElement e, string instructions)
    {
        var criteria = e.TryGetProperty("criteria", out var c) && c.ValueKind == JsonValueKind.Object ? c : default;
        return criteria.ValueKind == JsonValueKind.Object
            ? new Noul(instructions, OptString(criteria, "true"), OptString(criteria, "false"))
            : new Noul(instructions);
    }

    private static string? OptString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

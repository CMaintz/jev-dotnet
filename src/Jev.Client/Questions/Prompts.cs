using System.Text.Json;
using Jev.Json;

namespace Jev;

/// <summary>
/// Validates and freezes question instructions and criteria. Each is text, or structured
/// JSON: any value the serializer writes as a JSON object or array (an anonymous object,
/// a dictionary, a list, a <see cref="JsonElement"/>). The result is a detached
/// <see cref="JsonElement"/>, so a caller's later changes never reach the question. Any
/// <see cref="Image"/> inside is written as its reference and added to the given set.
/// </summary>
internal static class Prompts
{
    /// <summary>The API's nesting limit for each of instructions and criteria.</summary>
    private const int MaxDepth = 64;

    // Deeper than the API allows, so the explicit depth check reports the limit, not the serializer.
    private static readonly JsonSerializerOptions Options = new(JevJson.Options) { MaxDepth = MaxDepth * 2 };

    /// <summary>Instructions: non-blank text, or a non-empty JSON object or array.</summary>
    public static JsonElement Require(object? value, string name, ImageSet images) => Require(value, name, 0, images);

    /// <summary>
    /// One criteria value (an option, a level, a gloss). It already sits one level inside
    /// <c>criteria</c>, which the 64-level limit applies to as a whole.
    /// </summary>
    public static JsonElement Criterion(object? value, string name, ImageSet images) => Require(value, name, 1, images);

    /// <summary>Like <see cref="Criterion"/> but null stays null, for optional glosses.</summary>
    public static JsonElement? OptionalCriterion(object? value, string name, ImageSet images) =>
        value is null ? null : Criterion(value, name, images);

    /// <summary>
    /// Whether two values mean the same JSON: object keys in any order, numbers by value
    /// (<c>2</c> equals <c>2.0</c>), as jev-eval itself compares definitions.
    /// </summary>
    public static bool Same(JsonElement left, JsonElement right) =>
        left.ValueKind == right.ValueKind && left.ValueKind switch
        {
            JsonValueKind.Object => SameObject(left, right),
            JsonValueKind.Array => left.GetArrayLength() == right.GetArrayLength()
                && left.EnumerateArray().Zip(right.EnumerateArray()).All(pair => Same(pair.First, pair.Second)),
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.GetDouble() == right.GetDouble(),
            _ => true,
        };

    /// <summary>A hash consistent with <see cref="Same"/>: coarse, but order- and spelling-independent.</summary>
    public static int Hash(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => HashCode.Combine(value.ValueKind, value.EnumerateObject().Count()),
        JsonValueKind.Array => HashCode.Combine(value.ValueKind, value.GetArrayLength()),
        JsonValueKind.String => HashCode.Combine(value.ValueKind, value.GetString()),
        JsonValueKind.Number => HashCode.Combine(value.ValueKind, value.GetDouble()),
        _ => value.ValueKind.GetHashCode(),
    };

    private static JsonElement Require(object? value, string name, int depth, ImageSet images)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        var element = Serialize(value, name, images);
        var usable = element.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(element.GetString()),
            JsonValueKind.Object => element.EnumerateObject().Any(),
            JsonValueKind.Array => element.GetArrayLength() > 0,
            _ => false,
        };
        if (!usable)
        {
            throw new ArgumentException($"{name} must be non-blank text, or a non-empty JSON object or array.", name);
        }

        if (depth + Depth(element) > MaxDepth)
        {
            throw new ArgumentException($"{name} nests deeper than {MaxDepth} levels.", name);
        }

        return element;
    }

    // Re-serializing a JsonElement too yields compact JSON, so equal content compares equal.
    private static JsonElement Serialize(object value, string name, ImageSet images)
    {
        try
        {
            using var collector = ImageCollector.Into(images);
            return JsonSerializer.SerializeToElement(value, Options);
        }
        catch (Exception e) when (e is NotSupportedException or JsonException or ArgumentException or InvalidOperationException)
        {
            throw new ArgumentException($"{name} could not be written as JSON: {e.Message}", name, e);
        }
    }

    private static bool SameObject(JsonElement left, JsonElement right)
    {
        var properties = left.EnumerateObject().ToList();
        return properties.Count == right.EnumerateObject().Count()
            && properties.All(p => right.TryGetProperty(p.Name, out var other) && Same(p.Value, other));
    }

    private static int Depth(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => 1 + e.EnumerateObject().Select(p => Depth(p.Value)).DefaultIfEmpty(0).Max(),
        JsonValueKind.Array => 1 + e.EnumerateArray().Select(Depth).DefaultIfEmpty(0).Max(),
        _ => 0,
    };
}

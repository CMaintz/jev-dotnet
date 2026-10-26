using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;

namespace Jev.Json;

/// <summary>
/// Decodes a <c>GET /v1/models</c> response. A body that is not JSON, has no
/// <c>models</c> array, or has an entry missing a string field or carrying a date that is
/// not <c>YYYY-MM-DD</c> surfaces as a <see cref="JevException"/> carrying the raw body.
/// </summary>
internal sealed class ModelsReader
{
    private readonly string _body;

    private ModelsReader(string body) => _body = body;

    public static IReadOnlyList<ModelInfo> Parse(string body) => new ModelsReader(body).Parse();

    private ReadOnlyCollection<ModelInfo> Parse()
    {
        using var doc = ParseDocument();
        var root = doc.RootElement;
        var models = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("models", out var found) ? found : default;
        if (models.ValueKind != JsonValueKind.Array)
        {
            throw Malformed("no \"models\" array");
        }

        return Array.AsReadOnly(models.EnumerateArray().Select(ReadModel).ToArray());
    }

    private JsonDocument ParseDocument()
    {
        try
        {
            return JsonDocument.Parse(_body, JevJson.ReadOptions);
        }
        catch (JsonException e)
        {
            throw new JevException($"Malformed response: {e.Message}", responseBody: _body, inner: e);
        }
    }

    private ModelInfo ReadModel(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            throw Malformed("a model is not a JSON object");
        }

        return new ModelInfo(RequireString(e, "name"), RequireString(e, "description"), ReadDate(e));
    }

    private DateOnly ReadDate(JsonElement e)
    {
        var text = RequireString(e, "release_date");
        return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw Malformed($"release_date \"{text}\" is not YYYY-MM-DD");
    }

    private string RequireString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()!
            : throw Malformed($"missing string field '{name}'");

    private JevException Malformed(string detail) => new($"Malformed response: {detail}.", responseBody: _body);
}

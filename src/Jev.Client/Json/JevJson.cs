using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jev.Json;

/// <summary>Shared serializer options for encoding user-supplied <c>state</c>.</summary>
internal static class JevJson
{
    /// <summary>
    /// Null members are dropped. Structured state is written with the default naming
    /// policy: dictionary keys pass through verbatim, so a <c>Dictionary&lt;string, object&gt;</c>
    /// gives exact control over wire field names.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// For reading responses and thresholds files: twice the API's 64 levels per field,
    /// since a legend or a recorded definition sits a few levels below the root.
    /// </summary>
    public static readonly JsonDocumentOptions ReadOptions = new() { MaxDepth = 128 };
}

using System.Net.Http.Headers;
using System.Text;

namespace Jev.Client.Tests;

/// <summary>One part of a multipart/form-data body: its headers and its bytes.</summary>
internal sealed record MultipartPart(IReadOnlyDictionary<string, string> Headers, byte[] Body)
{
    public string Text => Encoding.UTF8.GetString(Body);

    /// <summary>The <c>name</c> parameter of the part's Content-Disposition, unquoted.</summary>
    public string? Name => Disposition?.Name?.Trim('"');

    public string? FileName => Disposition?.FileName?.Trim('"');

    public string? ContentType => Headers.GetValueOrDefault("Content-Type");

    private ContentDispositionHeaderValue? Disposition =>
        Headers.TryGetValue("Content-Disposition", out var value) ? ContentDispositionHeaderValue.Parse(value) : null;
}

/// <summary>
/// A minimal multipart/form-data splitter, so tests read a request body the way a server
/// would without pulling in ASP.NET's reader.
/// </summary>
internal static class MultipartParts
{
    public static List<MultipartPart> Parse(byte[] body, string contentType)
    {
        var boundary = MediaTypeHeaderValue.Parse(contentType).Parameters.Single(p => p.Name == "boundary").Value!.Trim('"');
        var delimiter = Encoding.ASCII.GetBytes("--" + boundary);
        var parts = new List<MultipartPart>();
        var start = IndexOf(body, delimiter, 0) + delimiter.Length;
        while (!body.AsSpan(start).StartsWith("--"u8))
        {
            var end = IndexOf(body, delimiter, start);
            parts.Add(ReadPart(body[(start + 2)..(end - 2)]));
            start = end + delimiter.Length;
        }

        return parts;
    }

    private static MultipartPart ReadPart(byte[] part)
    {
        var split = IndexOf(part, "\r\n\r\n"u8.ToArray(), 0);
        var headers = Encoding.UTF8.GetString(part, 0, split)
            .Split("\r\n")
            .Select(line => line.Split(':', 2))
            .ToDictionary(kv => kv[0].Trim(), kv => kv[1].Trim(), StringComparer.OrdinalIgnoreCase);
        return new MultipartPart(headers, part[(split + 4)..]);
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int from)
    {
        var found = haystack.AsSpan(from).IndexOf(needle);
        return found < 0 ? throw new FormatException("Delimiter not found in multipart body.") : from + found;
    }
}

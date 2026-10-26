using System.Text.Json;

namespace Jev.Json;

/// <summary>The JSON request body and the distinct images it lists in <c>assets</c>, in part order.</summary>
internal sealed record WrittenRequest(byte[] Json, IReadOnlyList<Image> Images);

/// <summary>
/// Encodes a <c>system_one</c> request body. Written by hand (not reflection) because
/// the <c>criteria</c> field is a map for a Choice but an array for a Score.
/// </summary>
internal static class RequestWriter
{
    public static WrittenRequest Serialize(object state, string model, IReadOnlyDictionary<string, Question> questions)
    {
        var images = new ImageSet();
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("model", model);
            w.WritePropertyName("state");
            WriteState(w, state, images);
            w.WritePropertyName("questions");
            WriteQuestions(w, questions);
            images.AddRange(questions.Values.SelectMany(q => q.Images.Items));
            WriteAssets(w, images.Items);
            w.WriteEndObject();
        }

        return new WrittenRequest(buffer.ToArray(), images.Items);
    }

    /// <summary>The multipart part name that holds the image at <paramref name="index"/> in <see cref="WrittenRequest.Images"/>.</summary>
    public static string PartName(int index) => $"image-{index + 1}";

    private static void WriteAssets(Utf8JsonWriter w, IReadOnlyList<Image> images)
    {
        if (images.Count == 0)
        {
            return;
        }

        w.WriteStartObject("assets");
        for (var i = 0; i < images.Count; i++)
        {
            w.WriteStartObject(images[i].Reference);
            w.WriteString("type", "image");
            w.WriteString("part", PartName(i));
            w.WriteEndObject();
        }

        w.WriteEndObject();
    }

    private static void WriteState(Utf8JsonWriter w, object state, ImageSet images)
    {
        try
        {
            using var collector = ImageCollector.Into(images);
            JsonSerializer.Serialize(w, state, JevJson.Options);
        }
        catch (Exception e) when (e is NotSupportedException or JsonException or ArgumentException or InvalidOperationException)
        {
            throw new ArgumentException($"The state could not be written as JSON: {e.Message}", nameof(state), e);
        }
    }

    private static void WriteQuestions(Utf8JsonWriter w, IReadOnlyDictionary<string, Question> questions)
    {
        w.WriteStartObject();
        foreach (var (id, question) in questions)
        {
            w.WritePropertyName(id);
            WriteQuestion(w, question);
        }

        w.WriteEndObject();
    }

    private static void WriteQuestion(Utf8JsonWriter w, Question question)
    {
        w.WriteStartObject();
        w.WriteString("type", WireType(question));
        w.WritePropertyName("instructions");
        question.Instructions.WriteTo(w);
        WriteCriteria(w, question);
        w.WriteEndObject();
    }

    private static string WireType(Question question) => question switch
    {
        Choice => "choice",
        Score => "score",
        Noul => "noul",
        Box => "box",
        _ => throw new ArgumentException($"Unknown question type {question.GetType().Name}.", nameof(question)),
    };

    private static void WriteCriteria(Utf8JsonWriter w, Question question)
    {
        switch (question)
        {
            case Choice choice:
                WriteMapCriteria(w, choice.Criteria);
                break;
            case Score score:
                WriteArrayCriteria(w, score.Criteria);
                break;
            case Noul noul:
                WriteNoulCriteria(w, noul);
                break;
        }
    }

    private static void WriteMapCriteria(Utf8JsonWriter w, IReadOnlyDictionary<string, JsonElement> criteria)
    {
        w.WritePropertyName("criteria");
        w.WriteStartObject();
        foreach (var (option, description) in criteria)
        {
            w.WritePropertyName(option);
            description.WriteTo(w);
        }

        w.WriteEndObject();
    }

    private static void WriteArrayCriteria(Utf8JsonWriter w, IReadOnlyList<JsonElement> levels)
    {
        w.WritePropertyName("criteria");
        w.WriteStartArray();
        foreach (var level in levels)
        {
            level.WriteTo(w);
        }

        w.WriteEndArray();
    }

    private static void WriteNoulCriteria(Utf8JsonWriter w, Noul noul)
    {
        if (noul.WhenTrue is null && noul.WhenFalse is null)
        {
            return;
        }

        w.WritePropertyName("criteria");
        w.WriteStartObject();
        if (noul.WhenTrue is { } whenTrue)
        {
            w.WritePropertyName("true");
            whenTrue.WriteTo(w);
        }

        if (noul.WhenFalse is { } whenFalse)
        {
            w.WritePropertyName("false");
            whenFalse.WriteTo(w);
        }

        w.WriteEndObject();
    }
}

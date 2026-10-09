using System.Text.Json;

namespace Jev.Json;

/// <summary>
/// Encodes a <c>system_one</c> request body. Written by hand (not reflection) because
/// the <c>criteria</c> field is a map for a Choice but an array for a Score.
/// </summary>
internal static class RequestWriter
{
    public static byte[] Serialize(object state, string model, IReadOnlyDictionary<string, Question> questions)
    {
        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("model", model);
            w.WritePropertyName("state");
            WriteState(w, state);
            w.WritePropertyName("questions");
            WriteQuestions(w, questions);
            w.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private static void WriteState(Utf8JsonWriter w, object state)
    {
        try
        {
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
        w.WriteString("instructions", question.Instructions);
        WriteCriteria(w, question);
        w.WriteEndObject();
    }

    private static string WireType(Question question) => question switch
    {
        Choice => "choice",
        Score => "score",
        Noul => "noul",
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

    private static void WriteMapCriteria(Utf8JsonWriter w, IReadOnlyDictionary<string, string> criteria)
    {
        w.WritePropertyName("criteria");
        w.WriteStartObject();
        foreach (var (option, description) in criteria)
        {
            w.WriteString(option, description);
        }

        w.WriteEndObject();
    }

    private static void WriteArrayCriteria(Utf8JsonWriter w, IReadOnlyList<string> levels)
    {
        w.WritePropertyName("criteria");
        w.WriteStartArray();
        foreach (var level in levels)
        {
            w.WriteStringValue(level);
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
        if (noul.WhenTrue is not null)
        {
            w.WriteString("true", noul.WhenTrue);
        }

        if (noul.WhenFalse is not null)
        {
            w.WriteString("false", noul.WhenFalse);
        }

        w.WriteEndObject();
    }
}

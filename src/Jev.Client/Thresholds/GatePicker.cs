namespace Jev;

/// <summary>
/// Picks the gate for a caller's questions, matching jev-sort 1.2.0: one choice/score
/// question uses its own gate, several use the composite gate measured on exactly those
/// questions. Nouls are never gated (their jev-eval threshold is not a confidence).
/// </summary>
internal static class GatePicker
{
    private const string Composite = "composite";

    public static PickedGate Pick(JevThresholds file, IReadOnlyDictionary<string, Question> questions, string? model)
    {
        var ids = GatedIds(questions);
        var (gate, source) = ids.Count switch
        {
            0 => throw new JevThresholdsException("Nothing to gate on (every question is a noul)."),
            1 => (QuestionGate(file, ids[0]), ids[0]),
            _ => (CompositeGate(file, ids), Composite),
        };
        return new PickedGate(gate, source, file.Model, ids, Warnings(file, questions, ids, model));
    }

    /// <summary>The ids of the choice and score questions, in ordinal order.</summary>
    private static List<string> GatedIds(IReadOnlyDictionary<string, Question> questions) =>
        questions.Where(kv => kv.Value is not Noul).Select(kv => kv.Key).Order(StringComparer.Ordinal).ToList();

    private static ThresholdGate QuestionGate(JevThresholds file, string id) =>
        file.Questions.TryGetValue(id, out var entry)
            ? entry.Gate
            : throw new JevThresholdsException(
                $"The thresholds file has no gate for \"{id}\" (jev-eval refused it or found no gate meeting the goal; see its report).");

    private static ThresholdGate CompositeGate(JevThresholds file, List<string> ids)
    {
        var composite = file.Composite ?? throw new JevThresholdsException(
            $"The thresholds file has no composite gate for [{string.Join(", ", ids)}] "
            + "(jev-eval refused or found the row gate unstable; see its report).");
        var measured = composite.Questions.Order(StringComparer.Ordinal).ToList();
        if (!measured.SequenceEqual(ids, StringComparer.Ordinal))
        {
            throw new JevThresholdsException(
                $"The thresholds file gates [{string.Join(", ", measured)}] but these questions gate "
                + $"[{string.Join(", ", ids)}]; re-run jev-eval thresholds with these questions.");
        }

        return composite.Gate;
    }

    private static List<string> Warnings(
        JevThresholds file, IReadOnlyDictionary<string, Question> questions, List<string> ids, string? model)
    {
        var warnings = new List<string>();
        if (model is not null && model != file.Model)
        {
            warnings.Add($"Thresholds were measured on {file.Model} but you use {model}; re-measure after a model change.");
        }

        warnings.AddRange(ids.Where(id => Reworded(file, id, questions[id])).Select(id => $"{id} was reworded since it was measured; re-measure."));
        return warnings;
    }

    private static bool Reworded(JevThresholds file, string id, Question question)
    {
        if (file.Definitions is null)
        {
            return false;
        }

        if (file.UnreadableDefinitions.Contains(id))
        {
            return true;
        }

        return file.Definitions.TryGetValue(id, out var measured) && !measured.Equals(question);
    }
}

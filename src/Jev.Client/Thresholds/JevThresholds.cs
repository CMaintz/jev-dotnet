using System.Collections.ObjectModel;
using Jev.Json;

namespace Jev;

/// <summary>
/// A jev-eval <c>thresholds.json</c> (contract version 1): the confidence gates jev-eval
/// measured on labeled data. Read one with <see cref="Load"/> or <see cref="Parse"/>, then
/// call <see cref="Pick"/> with the questions you send to get the gate that applies to them.
/// Unknown keys in the file are ignored.
/// </summary>
public sealed class JevThresholds
{
    internal JevThresholds(
        string model,
        IDictionary<string, QuestionGate> questions,
        CompositeGate? composite,
        IDictionary<string, Question>? definitions,
        IReadOnlySet<string> unreadableDefinitions)
    {
        Model = model;
        Questions = new ReadOnlyDictionary<string, QuestionGate>(questions);
        Composite = composite;
        Definitions = definitions is null ? null : new ReadOnlyDictionary<string, Question>(definitions);
        UnreadableDefinitions = unreadableDefinitions;
    }

    /// <summary>The model the gates were measured on, e.g. <c>jev-latest</c>.</summary>
    public string Model { get; }

    /// <summary>
    /// Per-question gates keyed by question id. Questions jev-eval refused (too few labels,
    /// or no gate meeting the goal) are absent. A noul entry gates <c>|p - 0.5| * 2</c>, not
    /// a confidence, so <see cref="Pick"/> never uses it.
    /// </summary>
    public IReadOnlyDictionary<string, QuestionGate> Questions { get; }

    /// <summary>The row-level gate over several choice/score questions, or null when jev-eval did not produce one.</summary>
    public CompositeGate? Composite { get; }

    /// <summary>
    /// The exact question jev-eval sent for each id, or null for files written before
    /// jev-eval 1.2.0. Definitions that could not be read are left out.
    /// </summary>
    public IReadOnlyDictionary<string, Question>? Definitions { get; }

    /// <summary>Ids whose definition is present but not a valid question; treated as reworded.</summary>
    internal IReadOnlySet<string> UnreadableDefinitions { get; }

    /// <summary>Read and parse the thresholds file at <paramref name="path"/>.</summary>
    /// <exception cref="JevThresholdsException">The file is malformed or not contract version 1.</exception>
    /// <exception cref="IOException">The file could not be read, e.g. it does not exist.</exception>
    public static JevThresholds Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ThresholdsReader.Parse(File.ReadAllText(path), path);
    }

    /// <summary>Parse thresholds JSON text.</summary>
    /// <exception cref="JevThresholdsException">The JSON is malformed or not contract version 1.</exception>
    public static JevThresholds Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return ThresholdsReader.Parse(json, "thresholds file");
    }

    /// <summary>
    /// The gate for <paramref name="questions"/>: with one choice/score question, that
    /// question's own gate; with several, the <see cref="Composite"/> gate, which must have
    /// been measured on exactly those questions. Nouls are ignored. A model mismatch or a
    /// question reworded since it was measured is reported in
    /// <see cref="PickedGate.Warnings"/> rather than thrown.
    /// </summary>
    /// <param name="questions">The questions you send to Jev, keyed by id.</param>
    /// <param name="model">The model you call; null skips the model check.</param>
    /// <exception cref="JevThresholdsException">No gate in the file fits these questions.</exception>
    public PickedGate Pick(IReadOnlyDictionary<string, Question> questions, string? model = null)
    {
        ArgumentNullException.ThrowIfNull(questions);
        return GatePicker.Pick(this, questions, model);
    }
}

using System.Globalization;

namespace Jev;

/// <summary>
/// The gate that applies to your questions, picked by <see cref="JevThresholds.Pick"/>,
/// with where it came from and what jev-eval measured it buys. Log
/// <see cref="Describe"/> and <see cref="Warnings"/> next to your results.
/// </summary>
public sealed class PickedGate
{
    internal PickedGate(ThresholdGate gate, string source, string model, IList<string> questions, IList<string> warnings)
    {
        Gate = gate;
        Source = source;
        Model = model;
        Questions = Array.AsReadOnly(questions.ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
    }

    /// <summary>The cut-point and its measured accuracy, coverage and sample size.</summary>
    public ThresholdGate Gate { get; }

    /// <summary>The confidence cut-point: escalate below it.</summary>
    public double Threshold => Gate.Threshold;

    /// <summary><c>composite</c> for the row-level gate, else the question id the gate came from.</summary>
    public string Source { get; }

    /// <summary>The model the gate was measured on.</summary>
    public string Model { get; }

    /// <summary>The choice and score question ids the gate applies to, in ordinal order.</summary>
    public IReadOnlyList<string> Questions { get; }

    /// <summary>
    /// Soft problems that do not stop the gate from being used: a different model than
    /// the one measured, or a question reworded since it was measured. Empty when none.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// The row confidence the gate applies to: the minimum <see cref="CalibratedAnswer.Confidence"/>
    /// across the gated questions. Null when a gated answer is missing, is not a choice or
    /// score answer, or has no confidence.
    /// </summary>
    public double? RowConfidence(IReadOnlyDictionary<string, Answer> answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        double? min = null;
        foreach (var id in Questions)
        {
            if (!answers.TryGetValue(id, out var answer) || answer is not CalibratedAnswer { Confidence: { } confidence })
            {
                return null;
            }

            min = Math.Min(min ?? confidence, confidence);
        }

        return min;
    }

    /// <summary>
    /// True when the row should go to a person or a bigger model: its
    /// <see cref="RowConfidence"/> is below <see cref="Threshold"/>, or unknown. Unlike
    /// jev-core's <c>minConfidence</c>, a missing confidence escalates rather than passes,
    /// matching <see cref="CalibratedAnswer.IsConfident"/>.
    /// </summary>
    public bool ShouldEscalate(IReadOnlyDictionary<string, Answer> answers) =>
        RowConfidence(answers) is not { } confidence || confidence < Threshold;

    /// <summary>Same as <see cref="ShouldEscalate(IReadOnlyDictionary{string, Answer})"/> for a whole response.</summary>
    public bool ShouldEscalate(SystemOneResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ShouldEscalate(response.Answers);
    }

    /// <summary>One line naming the gate, where it came from and what jev-eval measured, for logs.</summary>
    public string Describe()
    {
        var from = Source == "composite" ? "composite" : $"\"{Source}\"";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"gate {Gate.Threshold} from {from} (jev-eval: {Gate.Accuracy * 100:0.0}% accuracy at {Gate.Coverage * 100:0.0}% coverage, n={Gate.N}, model {Model})");
    }
}

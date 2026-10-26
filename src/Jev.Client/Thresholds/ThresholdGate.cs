using System.Globalization;

namespace Jev;

/// <summary>
/// The confidence gate picked by <see cref="JevThresholds.Pick"/> for your questions, with
/// what jev-eval measured it buys. A row is covered (decided automatically) when its
/// confidence is at or above <see cref="Threshold"/>; below it, escalate. Row confidence is
/// the lowest choice/score confidence across <see cref="QuestionIds"/>; nouls and boxes
/// never take part. Log <see cref="Describe"/> and <see cref="Warnings"/> next to your results.
/// </summary>
public sealed record ThresholdGate
{
    /// <summary>The <see cref="Source"/> of a row-level gate measured over several questions.</summary>
    public const string Composite = "composite";

    internal ThresholdGate(MeasuredGate measured, string source, string model, IList<string> questionIds, IList<string> warnings)
    {
        (Threshold, Accuracy, Coverage, N) = (measured.Threshold, measured.Accuracy, measured.Coverage, measured.N);
        Source = source;
        Model = model;
        QuestionIds = Array.AsReadOnly(questionIds.ToArray());
        Warnings = Array.AsReadOnly(warnings.ToArray());
    }

    /// <summary>The confidence cut-point: escalate below it.</summary>
    public double Threshold { get; }

    /// <summary>Accuracy jev-eval measured on covered unseen rows, in [0, 1].</summary>
    public double Accuracy { get; }

    /// <summary>Share of unseen rows the gate covered, in [0, 1].</summary>
    public double Coverage { get; }

    /// <summary>Number of labeled rows the measurement used.</summary>
    public int N { get; }

    /// <summary><see cref="Composite"/> for the row-level gate, else the question id it came from.</summary>
    public string Source { get; }

    /// <summary>The choice and score question ids the gate applies to, in ordinal order.</summary>
    public IReadOnlyList<string> QuestionIds { get; }

    /// <summary>The model jev-eval measured on.</summary>
    public string Model { get; }

    /// <summary>
    /// Soft problems that do not stop the gate from being used: a different model than the
    /// one measured, or a question reworded since it was measured. Empty when none.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// The row confidence: the minimum <see cref="CalibratedAnswer.Confidence"/> across
    /// <see cref="QuestionIds"/>. Null when a gated answer is missing, is not a choice or
    /// score answer, or has no confidence.
    /// </summary>
    public double? RowConfidence(SystemOneResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        double? min = null;
        foreach (var id in QuestionIds)
        {
            if (!response.Answers.TryGetValue(id, out var answer) || answer is not CalibratedAnswer { Confidence: { } confidence })
            {
                return null;
            }

            min = Math.Min(min ?? confidence, confidence);
        }

        return min;
    }

    /// <summary>
    /// True when the row should go to a person or a bigger model: its
    /// <see cref="RowConfidence"/> is below <see cref="Threshold"/>, or unknown. Fails toward
    /// review: a missing answer or confidence escalates rather than throwing.
    /// </summary>
    public bool ShouldEscalate(SystemOneResponse response) =>
        RowConfidence(response) is not { } confidence || confidence < Threshold;

    /// <summary>One line naming the gate, where it came from and what jev-eval measured, for logs.</summary>
    public string Describe()
    {
        var from = Source == Composite ? Composite : $"\"{Source}\"";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"gate {Threshold} from {from} (jev-eval: {Accuracy * 100:0.0}% accuracy at {Coverage * 100:0.0}% coverage, n={N}, model {Model})");
    }

    /// <inheritdoc />
    public bool Equals(ThresholdGate? other) =>
        other is not null
        && (Threshold, Accuracy, Coverage, N, Source, Model) == (other.Threshold, other.Accuracy, other.Coverage, other.N, other.Source, other.Model)
        && ContentEquality.SameItems(QuestionIds, other.QuestionIds)
        && ContentEquality.SameItems(Warnings, other.Warnings);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Threshold, Source, Model, QuestionIds.Count);
}

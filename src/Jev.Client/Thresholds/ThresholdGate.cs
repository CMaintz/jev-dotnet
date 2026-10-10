namespace Jev;

/// <summary>
/// One gate measured by jev-eval: answers at or above <see cref="Threshold"/> are covered
/// (decided automatically), the rest escalate. <see cref="Accuracy"/> and
/// <see cref="Coverage"/> are what that cut-point achieved on <see cref="N"/> unseen rows.
/// </summary>
/// <param name="Threshold">The confidence cut-point in [0, 1].</param>
/// <param name="Accuracy">Accuracy of the covered rows, in [0, 1].</param>
/// <param name="Coverage">Share of rows covered by the gate, in [0, 1].</param>
/// <param name="N">How many labeled rows the gate was measured on.</param>
public sealed record ThresholdGate(double Threshold, double Accuracy, double Coverage, int N);

/// <summary>A per-question gate from jev-eval's <c>questions</c> block.</summary>
/// <param name="Type">The question type jev-eval measured: <c>choice</c>, <c>score</c> or <c>noul</c>.</param>
/// <param name="Gate">The measured gate.</param>
public sealed record QuestionGate(string Type, ThresholdGate Gate);

/// <summary>
/// jev-eval's row-level gate (<c>composite</c>): it applies to the minimum confidence across
/// the listed choice and score questions.
/// </summary>
public sealed record CompositeGate
{
    /// <param name="gate">The measured gate.</param>
    /// <param name="questions">The question ids whose minimum confidence the gate was measured on.</param>
    public CompositeGate(ThresholdGate gate, IReadOnlyList<string> questions)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(questions);
        Gate = gate;
        Questions = Array.AsReadOnly(questions.ToArray());
    }

    /// <summary>The measured gate.</summary>
    public ThresholdGate Gate { get; }

    /// <summary>The question ids whose minimum confidence the gate was measured on.</summary>
    public IReadOnlyList<string> Questions { get; }

    /// <inheritdoc />
    public bool Equals(CompositeGate? other) =>
        other is not null && Gate == other.Gate && ContentEquality.SameItems(Questions, other.Questions);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Gate, Questions.Count);
}

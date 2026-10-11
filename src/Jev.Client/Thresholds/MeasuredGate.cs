namespace Jev;

/// <summary>One gate as jev-eval measured it: the cut-point and what it bought on <see cref="N"/> unseen rows.</summary>
internal sealed record MeasuredGate(double Threshold, double Accuracy, double Coverage, int N);

/// <summary>A per-question gate from jev-eval's <c>questions</c> block; <see cref="Type"/> is choice, score or noul.</summary>
internal sealed record QuestionGate(string Type, MeasuredGate Gate);

/// <summary>jev-eval's row-level gate over the minimum confidence across <see cref="Questions"/>.</summary>
internal sealed record CompositeGate(MeasuredGate Gate, IReadOnlyList<string> Questions);

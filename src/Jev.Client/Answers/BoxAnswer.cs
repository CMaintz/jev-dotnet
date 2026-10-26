namespace Jev;

/// <summary>
/// The answer to a <see cref="Jev.Box"/>: each edge is a fraction from 0 to 1, measured from
/// the image's top-left corner. Multiply <see cref="Top"/> and <see cref="Bottom"/> by the
/// image's height, and <see cref="Left"/> and <see cref="Right"/> by its width, for pixels.
/// </summary>
/// <param name="Top">Top edge, as a fraction of the image's height.</param>
/// <param name="Bottom">Bottom edge, as a fraction of the image's height.</param>
/// <param name="Left">Left edge, as a fraction of the image's width.</param>
/// <param name="Right">Right edge, as a fraction of the image's width.</param>
/// <param name="Confidence">How sure the model is of the box, in [0, 1]; null when the service gives none.</param>
public sealed record BoxAnswer(double Top, double Bottom, double Left, double Right, double? Confidence) : Answer
{
    /// <summary>True when <see cref="Confidence"/> is at or above <paramref name="threshold"/>; false when it is null.</summary>
    public bool IsConfident(double threshold) => Confidence is { } c && c >= threshold;
}

namespace Jev;

/// <summary>
/// Find where something is in an image. The answer is the four edges of the smallest box
/// around the target, as fractions of the image's height and width from its top-left
/// corner. A Box takes no criteria, needs an image in the request, and always returns a
/// box, even for a target that is absent: ask a <see cref="Noul"/> alongside it to check.
/// </summary>
/// <remarks>
/// Needs Jev 1.14 or later (<c>jev-latest</c> and <c>jev-preview</c> both point there today).
/// TypeSafe calls Box "dreamer-ready": experimental, and the format and accuracy may change.
/// A request with a Box sends <c>TypeSafe-Danger-Zone-Images: true</c>. jev-eval does not
/// measure Box, so <see cref="JevThresholds.Pick"/> never gates on one.
/// </remarks>
public sealed record Box : Question
{
    /// <param name="instructions">What to find and in which image, e.g. "the coffee mug in `photo`"; text or structured JSON.</param>
    public Box(object instructions)
        : base(instructions)
    {
    }
}

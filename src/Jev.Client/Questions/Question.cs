using System.Text.Json;

namespace Jev;

/// <summary>
/// A single judgment to ask Jev about the shared <c>state</c>. Pick the subtype by
/// what the answer means: <see cref="Choice"/> for one of a defined set,
/// <see cref="Score"/> for a position on an ordered scale, <see cref="Noul"/> for a
/// yes/no probability, <see cref="Box"/> for where something is in an image. Independent questions sent together are evaluated in parallel.
/// </summary>
/// <remarks>
/// Instructions and criteria are text, or structured JSON when definitions, contrasts
/// or examples make the question clearer: pass any value that serializes to a JSON
/// object or array, such as an anonymous object, a dictionary or a list. It is copied
/// when the question is built, so later changes to it are not sent. Questions compare
/// by content.
/// </remarks>
public abstract record Question
{
    private protected Question(object instructions)
    {
        Instructions = Prompts.Require(instructions, nameof(instructions), Images);
    }

    /// <summary>
    /// The images in the instructions and criteria. The frozen JSON holds only their
    /// references, so the question keeps the images themselves for the request's assets.
    /// </summary>
    internal ImageSet Images { get; } = new();

    /// <summary>
    /// The judgment to make: a JSON string, or a structured object or array. Reference
    /// nested state with backticked paths such as <c>ticket.messages[0].text</c>.
    /// </summary>
    public JsonElement Instructions { get; }

    /// <inheritdoc />
    public virtual bool Equals(Question? other) =>
        other is not null && EqualityContract == other.EqualityContract && Prompts.Same(Instructions, other.Instructions);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(EqualityContract, Prompts.Hash(Instructions));
}

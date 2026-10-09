namespace Jev;

/// <summary>
/// A single judgment to ask Jev about the shared <c>state</c>. Pick the subtype by
/// what the answer means: <see cref="Choice"/> for one of a defined set,
/// <see cref="Score"/> for a position on an ordered scale, <see cref="Noul"/> for a
/// yes/no probability. Independent questions sent together are evaluated in parallel.
/// </summary>
public abstract record Question
{
    private protected Question(string instructions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        Instructions = instructions;
    }

    /// <summary>The judgment to make, in natural language. Reference nested state with backticked paths such as <c>ticket.messages[0].text</c>.</summary>
    public string Instructions { get; }
}

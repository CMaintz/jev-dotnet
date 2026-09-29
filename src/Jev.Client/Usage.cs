namespace Jev;

/// <summary>Token accounting for a single request.</summary>
public sealed record Usage
{
    /// <summary>Tokens consumed by the state and questions.</summary>
    public int InputTokens { get; init; }

    /// <summary>Tokens produced across all answers.</summary>
    public int OutputTokens { get; init; }
}

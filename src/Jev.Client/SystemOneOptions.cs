namespace Jev;

/// <summary>Per-call settings for one <see cref="JevClient.SystemOneAsync(object, IReadOnlyDictionary{string, Question}, SystemOneOptions, CancellationToken)"/> request.</summary>
public sealed record SystemOneOptions
{
    /// <summary>The model for this call, e.g. <c>jev-preview</c> or a pinned version; null uses <see cref="JevClientOptions.Model"/>.</summary>
    public string? Model { get; init; }

    /// <summary>
    /// Raise this request's context limits by sending <c>TypeSafe-Danger-Zone-Long-Context: true</c>.
    /// TypeSafe advises turning it on only for requests that need it.
    /// </summary>
    public bool LongContext { get; init; }
}

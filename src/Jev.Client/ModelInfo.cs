namespace Jev;

/// <summary>A model or alias the account can use, as listed by <see cref="JevClient.ModelsAsync"/>.</summary>
/// <param name="Name">The name to pass as <see cref="JevClientOptions.Model"/>, e.g. <c>jev-latest</c>.</param>
/// <param name="Description">Human-readable description of the model and its capabilities.</param>
/// <param name="ReleaseDate">The model's release date.</param>
public sealed record ModelInfo(string Name, string Description, DateOnly ReleaseDate);

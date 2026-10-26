namespace Jev;

/// <summary>Distinct images in first-seen order. Distinct by identity, as <see cref="Image"/> compares.</summary>
internal sealed class ImageSet
{
    private readonly List<Image> _ordered = [];
    private readonly HashSet<Image> _seen = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyList<Image> Items => _ordered;

    public void Add(Image image)
    {
        if (_seen.Add(image))
        {
            _ordered.Add(image);
        }
    }

    public void AddRange(IEnumerable<Image> images)
    {
        foreach (var image in images)
        {
            Add(image);
        }
    }
}

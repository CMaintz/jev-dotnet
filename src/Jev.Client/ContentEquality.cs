namespace Jev;

/// <summary>
/// Content comparison for the collections inside the public records, whose synthesized
/// equality would otherwise compare the defensive copies by reference.
/// </summary>
internal static class ContentEquality
{
    public static bool SameItems<T>(IReadOnlyList<T> left, IReadOnlyList<T> right) => left.SequenceEqual(right);

    public static bool SameEntries<TValue>(IReadOnlyDictionary<string, TValue> left, IReadOnlyDictionary<string, TValue> right) =>
        left.Count == right.Count
        && left.All(kv => right.TryGetValue(kv.Key, out var value) && EqualityComparer<TValue>.Default.Equals(kv.Value, value));
}

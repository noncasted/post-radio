namespace Common;

public static class CollectionsExtensions
{
    public static void Shuffle<T>(this IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var random = System.Random.Shared;
            var j = random.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public static T Random<T>(this IReadOnlyList<T> source)
    {
        if (source == null || source.Count == 0)
            throw new ArgumentException("Collection is empty", nameof(source));

        var random = System.Random.Shared;
        var index = random.Next(0, source.Count);
        return source[index];
    }

    public static T2 Random<T1, T2>(this IReadOnlyDictionary<T1, T2> source)
    {
        if (source == null || source.Count == 0)
            throw new ArgumentException("Collection is empty", nameof(source));

        var random = System.Random.Shared;
        var index = random.Next(0, source.Count);

        return source.Values.ToList()[index];
    }
}
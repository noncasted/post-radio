namespace Frontend;

public static class CollectionExtensions
{
    public static void Shuffle<T>(this IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
namespace ScrumRetroApi.Common;

public static class ListExtensions
{
    /// <summary>Embaralha a ordem (usado no quadro e nas sugestões para impedir rastreio por ordem de inserção).</summary>
    public static List<T> Shuffled<T>(this IEnumerable<T> source)
    {
        var array = source.ToArray();
        Random.Shared.Shuffle(array);
        return array.ToList();
    }
}

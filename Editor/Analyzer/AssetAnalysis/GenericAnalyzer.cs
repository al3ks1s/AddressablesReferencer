using UnityEngine;

public class GenericAnalyzer<T> : IAssetAnalyzer<T>
{
    /// <inheritdoc />
    public string CreateAssetEntry<T>()
    {
        return string.Empty;
    }


    /// <inheritdoc />
    public string CreateAssetReference<T>()
    {
        return string.Empty;
    }
}

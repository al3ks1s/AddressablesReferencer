using UnityEngine;

public class GenericAnalyzer<T> : IAssetAnalyzer<T>
{
    /// <inheritdoc />
    public virtual string CreateAssetEntry<T>()
    {
        return string.Empty;
    }


    /// <inheritdoc />
    public virtual string CreateAssetReference<T>()
    {
        return string.Empty;
    }
}

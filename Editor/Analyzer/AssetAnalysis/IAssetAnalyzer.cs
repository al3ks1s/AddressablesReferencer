using UnityEngine;

public interface IAssetAnalyzer<T>
{

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public abstract string CreateAssetEntry<T>();

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public abstract string CreateAssetReference<T>();



}

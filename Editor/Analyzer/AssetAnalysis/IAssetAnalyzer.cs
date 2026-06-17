using UnityEngine;

public interface IAssetAnalyzer<T>
{

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public string CreateAssetEntry<T>();

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public string CreateAssetReference<T>();



}

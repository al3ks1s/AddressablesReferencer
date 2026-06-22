using System.Collections.Generic;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace AddressableReferencer.Editor.Analyzer
{
    public interface IAssetAnalyzer
    {


        public abstract (AddressableAssetEntry, List<ObjectMapping>) Analyze(long pathId);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public abstract AddressableAssetEntry CreateAssetEntry(string assetGUID, string[] labels = null, string assetPath = null);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public abstract List<ObjectMapping> CreateAssetReference();




    }
}
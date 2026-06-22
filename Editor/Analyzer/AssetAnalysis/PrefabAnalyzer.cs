using AssetsTools.NET.Extra;
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.U2D;

namespace AddressableReferencer.Editor.Analyzer
{
    public class PrefabAnalyzer : GenericAnalyzerT<GameObject>
    {

        public PrefabAnalyzer(BundleAnalyzer parentAnalyzer) : base(parentAnalyzer) { }

        public override (AddressableAssetEntry, List<ObjectMapping>) Analyze(long pathId, string assetPath)
        {
            return base.Analyze(pathId, assetPath);
        }

    }
}
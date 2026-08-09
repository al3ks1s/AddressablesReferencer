using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace AddressableReferencer.Editor.Analyzer.AssetAnalysis
{
    public class SpriteAnalyzer : GenericAnalyzerT<Sprite>
    {

        public SpriteAnalyzer(BundleAnalyzer parentAnalyzer) : base(parentAnalyzer) { }
        public override (AddressableAssetEntry, List<ObjectMapping>) Analyze(long pathId, string assetPath)
        {
            AddressableAssetEntry entry;
            List<ObjectMapping> mappings = new List<ObjectMapping>();

            (entry, mappings) = base.Analyze(pathId, assetPath);

            if (CheckMissingAsset(assetPath, pathId, out var assetGUID, out var newPath))
            { 
                Debug.LogWarning($"Couldn't find or create asset {assetPath}");
                return (null, null);
            }


            return (entry, mappings);
        }

    }
}
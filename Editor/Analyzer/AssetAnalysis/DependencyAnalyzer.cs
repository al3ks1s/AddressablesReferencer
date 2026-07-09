using AssetsTools.NET.Extra;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace AddressableReferencer.Editor.Analyzer.AssetAnalysis
{
    public class DependencyAnalyzer : GenericAnalyzer
    {
        public DependencyAnalyzer(BundleAnalyzer parentAnalyzer) : base(parentAnalyzer)
        { }

        public override (AddressableAssetEntry, List<ObjectMapping>) Analyze(long pathId, string assetPath)
        {

            AddressableAssetEntry entry;
            List<ObjectMapping> mappings = new List<ObjectMapping>();

            AssetExternal assetExt = AssetManager.GetExtAsset(CabFile, 0, pathId);

            List<string> possibleAssets = new List<string>();

            foreach (var d in AssetDatabase.GetDependencies(assetPath))
            {
                var tempObject = AssetDatabase.LoadMainAssetAtPath(d);

                if (!tempObject.name.Equals(assetExt.baseField["m_Name"].AsString))
                    continue;

                if (!tempObject.GetType().Name.Equals(assetExt.baseField.TypeName))
                    continue;

                possibleAssets.Add(d);
            }

            if (possibleAssets.Count > 1)
            {
                Debug.LogError($"Found too many possible assets for {assetExt.baseField["m_Name"].AsString} of type {assetExt.baseField.TypeName}. Using exact match on name, recheck in mapping window \n\n{string.Join("\n", possibleAssets)}");
                return (null, null);
            }

            if (possibleAssets.Count == 0)
            {

                // Create sub asset here if necessary

                Debug.LogWarning($"No possible assets for {assetExt.baseField["m_Name"].AsString} of type {assetExt.baseField.TypeName}, check if it exists");
                return (null, null);
            }

            // Debug.Log($"Asset {assetExt.baseField["m_Name"].AsString} of {assetPath} is at path {possibleAssets[0]}");

            string assetGuid = AssetDatabase.GUIDFromAssetPath(possibleAssets[0]).ToString();
            entry = CreateOrGetAssetEntry(assetGuid);
            var createdReference = CreateAssetReference(pathId, assetGuid, possibleAssets[0]);
            if (createdReference != null)
                mappings.Add(createdReference);

            return (entry, mappings);

        }

    }
}
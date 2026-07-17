using AssetsTools.NET.Extra;
using Steamworks;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.U2D;

namespace AddressableReferencer.Editor.Analyzer.AssetAnalysis
{
    public class PrefabAnalyzer : GenericAnalyzerT<GameObject>
    {

        public PrefabAnalyzer(BundleAnalyzer parentAnalyzer) : base(parentAnalyzer) { }

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

            GameObject mainObject = AssetDatabase.LoadMainAssetAtPath(newPath) as GameObject;
            mappings.AddRange(ProcessGameObject(mainObject, pathId));

            return (entry, mappings);
        }

        private List<ObjectMapping> ProcessGameObject(GameObject currentGO, long pathId)
        {

            List<ObjectMapping> objects = new List<ObjectMapping>();

            AssetDependencies deps = new(AssetManager, CabFile);
            var assetDeps = deps.FindImmediateDeps(pathId);
            AssetExternal mainAsset = AssetManager.GetExtAsset(CabFile, 0, pathId);

            if (currentGO.GetComponentCount() != assetDeps.InternalPaths.Count)
            {
                Debug.LogError($"Difference of component count for {currentGO.name} {pathId} in bundle {Path.GetFileNameWithoutExtension(Location.InternalId)}, the components will not be referenced. Details: {currentGO.GetType()} {currentGO.GetComponentCount()}/{assetDeps.InternalPaths.Count}");
                return objects;
            }

            for (int i = 0; i < currentGO.GetComponentCount(); i++)
            {
                var component = currentGO.GetComponentAtIndex(i);
                ObjectIdentifier.TryGetObjectIdentifier(component, out var objectId);

                if (!(component.GetType() == typeof(Transform)))
                    objects.Add(new ObjectMapping(objectId, assetDeps.InternalPaths.ToList()[i]));

            }

            var childGOs = deps.FindChildGO(pathId);

            if (currentGO.transform.childCount == childGOs.InternalPaths.Count)
            {
                for (int i = 0; i < currentGO.transform.childCount; i++)
                {
                    ObjectIdentifier.TryGetObjectIdentifier(currentGO.transform.GetChild(i).gameObject, out var objectId);
                    objects.Add(new ObjectMapping(objectId, childGOs.InternalPaths.ToList()[i]));
                    
                    objects.AddRange(ProcessGameObject(currentGO.transform.GetChild(i).gameObject, childGOs.InternalPaths.ToList()[i]));
                }
            }
            else
            {
                Debug.LogError($"Prefab Analysis for {currentGO.name} | {mainAsset.baseField["m_Name"].AsString} in bundle {Path.GetFileNameWithoutExtension(Location.InternalId)}: Theres a mismatching amount of child game objects between editor and bundle. Editor:{currentGO.transform.childCount} Bundle:{childGOs.InternalPaths.Count}");
            }

            return objects;
        }

    }
}
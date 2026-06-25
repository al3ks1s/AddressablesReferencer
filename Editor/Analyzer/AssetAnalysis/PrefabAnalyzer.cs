using AssetsTools.NET.Extra;
using Steamworks;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.U2D;

namespace AddressableReferencer.Editor.Analyzer
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

            mappings.AddRange(ProcessGameObject(newPath, mainObject, pathId));

            return (entry, mappings);

        }

        private List<ObjectMapping> ProcessGameObject(string assetPath, GameObject currentGO, long pathId)
        {

            List<ObjectMapping> objects = new List<ObjectMapping>();

            AssetDependencies deps = new(AssetManager, CabFile);
            var assetDeps = deps.FindImmediateDeps(pathId);
            AssetExternal mainAsset = AssetManager.GetExtAsset(CabFile, 0, pathId);

            if (currentGO.GetComponentCount() != assetDeps.InternalPaths.Count)
            {
                Debug.LogError($"Difference of component count for {assetPath} : {currentGO.GetType()} {currentGO.GetComponentCount()}/{assetDeps.InternalPaths.Count}");
                return objects;
            }

            for (int i = 0; i < currentGO.GetComponentCount(); i++)
            {
                var component = currentGO.GetComponentAtIndex(i);
                ObjectIdentifier.TryGetObjectIdentifier(component, out var objectId);

                if (!(component.GetType() == typeof(Transform)))
                    objects.Add(new ObjectMapping(objectId, pathId));
    
                Debug.Log($"Prefab object at {assetPath} : {component.name} {component.GetType()} {objectId.guid} {objectId.localIdentifierInFile}");

            }

            return objects;
        }

    }
}
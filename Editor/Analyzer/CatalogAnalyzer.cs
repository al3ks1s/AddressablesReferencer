using AddressableReferencer.Editor.Settings;
using AddressableReferencer.Editor.Utilities;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;
using static UnityEditor.FilePathAttribute;

namespace AddressableReferencer.Editor.Analyzer
{
    public class CatalogAnalyzer
    {

        public CatalogAnalyzer(string assetPath)
        {
            StreamingAssetsPath = assetPath;
        }

        public string StreamingAssetsPath { get; set; }

        internal IResourceLocator Locator { get; private set; }
        AsyncOperationHandle<IResourceLocator> LocatorHandle;

        List<IResourceLocation> bundles;

        List<IResourceLocation> labelBundles;
        List<IResourceLocation> separateBundles;
        List<IResourceLocation> togetherBundles;

        IResourceLocation monoscript;
        IResourceLocation unitybuiltins;

        List<(IResourceLocation, AddressableAssetGroup)> groupMapping = new();

        public bool LoadCatalog(string catalogPath)
        {

            if (StreamingAssetsPath.Equals(string.Empty))
                return false;

            using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker())
            {

                progressTracker.UpdateTask($"Loading the catalog");

                Addressables.InitializeAsync().WaitForCompletion();
                LocatorHandle = Addressables.LoadContentCatalogAsync(catalogPath);
                Locator = LocatorHandle.WaitForCompletion();

                bundles = Locator.AllLocations.Where(f => f.ProviderId == typeof(AssetBundleProvider).ToString()).ToList();
                monoscript = TryFindMonoscriptBundle();
                unitybuiltins = TryFindBuiltinAssetsBundle();

                IdentifyGroups();
            }

            return true;
        }

        public void UnloadCatalog()
        {
            Locator = null;
            LocatorHandle.Release();
        }

        public string TryFindCatalog(string catalogName = "catalog.bin")
        {

            if (!Directory.Exists(StreamingAssetsPath))
            {
                StreamingAssetsPath = string.Empty;
                return string.Empty;
            }

            foreach (string potentialCatalog in Directory.EnumerateFiles(StreamingAssetsPath, catalogName))
            {
                var catalogPath = Path.GetDirectoryName(potentialCatalog);

                if (File.Exists(potentialCatalog))
                {

                    if (!File.Exists(Path.Join(catalogPath, "catalog.hash")))
                        Debug.LogWarning($"Catalog path was found at {potentialCatalog} but no companion hash file");

                    return potentialCatalog;
                }
            }

            return string.Empty;

        }
        public IResourceLocation TryFindMonoscriptBundle()
        {
            // TODO - Find the bundle when the naming style is limited to the hash
            return bundles.Find(f => f.PrimaryKey.Contains("monoscripts"));
        }
        public IResourceLocation TryFindBuiltinAssetsBundle()
        {
            // TODO - Find the bundle when the naming style is limited to the hash
            return bundles.Find(f => f.PrimaryKey.Contains("unitybuiltinassets"));
        }

        public void IdentifyGroups()
        {

            /*

            Preprocess: 
            - replace("/", "_") 
            - replace("__", "_") to 
            - lookup and remove following regex : '_?[a-f0-9]{32}.bundle'
            - Split("_") For sorting
            - Split("_assets_") for group generation

            Sorting rules:

            - splitArray.size > 3 -> Packed Separately
            - splitArray.size = 3
                - label == "all" -> Packed together
                - label == anything else (even empty string) -> Packed by label
                - TODO: Adapt for multiple underscores in label (how?)

            - Default/Unknown -> Packed together 

            */

            groupMapping.Clear();
            labelBundles = new();
            togetherBundles = new();
            separateBundles = new();

            var bundlesToProcess = bundles.Where(b =>
            {
                if (b.PrimaryKey.Contains("monoscripts") || b.PrimaryKey.Contains("unitybuiltinassets"))
                    return false;

                var pkArray = b.PrimaryKey.Split("_");
                if (pkArray.Length < 2)
                    return true;

                // Filter out the obvious scene bundles because those cannot be referenced.
                return !pkArray[1].Equals("scenes");

            }).ToList();

            foreach (var bundle in bundlesToProcess)
            {

                string primaryKey = bundle.PrimaryKey.ToString().Replace("/", "_");
                primaryKey = Regex.Replace(primaryKey, "_?[0-9a-f]{32}.bundle", "");

                string[] primaryKeyComponents = primaryKey.Replace("__", "_").Split("_");
                string[] primaryKeyGroupLabelComp = primaryKey.Split("_assets_");

                if (primaryKeyComponents.Length == 1)
                { togetherBundles.Add(bundle); continue; }

                if (primaryKeyComponents.Length > 3)
                { separateBundles.Add(bundle); continue; }

                if (primaryKeyComponents.Length == 3 && primaryKeyGroupLabelComp.Last().Equals("all"))
                { togetherBundles.Add(bundle); continue; }

                if (primaryKeyComponents.Length == 3)
                { labelBundles.Add(bundle); continue; }

                togetherBundles.Add(bundle);

            }

            CreateLabelsAssetGroups();
            CreateSeparatelyPackedGroups();
            CreatePackedTogetherGroups();

        }

        public void CreateLabelsAssetGroups()
        {
            using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker())
            {

                progressTracker.UpdateTask($"Creating label groups");
                HashSet<string> labels = new HashSet<string>();
                HashSet<string> groups = new HashSet<string>();

                labels = labelBundles.Select(b => Regex.Replace(b.PrimaryKey.Split("_assets_").Last().Replace(".bundle", ""), "_[0-9a-f]{32}", "")).ToHashSet();
                labels = DeconcatenateLabels(labels);

                groups = labelBundles.Select(b => b.PrimaryKey.Split("_assets_").First()).ToHashSet();

                foreach (var label in labels)
                {
                    if (!label.Equals(""))
                        AddressableAssetSettingsDefaultObject.Settings.AddLabel(label);
                }

                foreach (var group in groups)
                {

                    var assetGroup = CreateOrGetGroup(group, BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel);
                    var groupBundles = labelBundles.Where(b => b.PrimaryKey.Split("/").Last().Split("_assets_").First().Equals(group));

                    foreach (var bun in groupBundles)
                    {
                        groupMapping.Add((bun, assetGroup));
                    }
                }
            }
        }
        public void CreateSeparatelyPackedGroups()
        {
            using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker())
            {

                progressTracker.UpdateTask($"Creating packed separately groups");
                HashSet<string> groups = separateBundles.Select(b => b.PrimaryKey.Split("_assets_").First()).ToHashSet();

                foreach (var group in groups)
                {

                    var assetGroup = CreateOrGetGroup(group, BundledAssetGroupSchema.BundlePackingMode.PackSeparately);
                    var groupBundles = separateBundles.Where(b => b.PrimaryKey.Split("_assets_").First().Equals(group));

                    foreach (var bun in groupBundles)
                    {
                        groupMapping.Add((bun, assetGroup));
                    }
                }
            }
        }
        public void CreatePackedTogetherGroups()
        {
            using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker())
            {

                progressTracker.UpdateTask($"Creating packed together groups");

                foreach (var group in togetherBundles)
                {
                    string groupName = group.PrimaryKey.Replace(".bundle", "").Split("_assets_").First();
                    var assetGroup = CreateOrGetGroup(groupName, BundledAssetGroupSchema.BundlePackingMode.PackTogether);
                    groupMapping.Add((group, assetGroup));
                }
            }
        }

        public void ProcessGroups()
        {
            List<Task> taskList = new();

            int counter = 0;

            foreach (var mapping in groupMapping)
            {
                using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker())
                {
                    progressTracker.UpdateTask($"({++counter}/{groupMapping.Count}) - Processing bundle : {Path.GetFileName(mapping.Item1.InternalId)}");
                    
                    BundleAnalyzer ba = new BundleAnalyzer(
                        mapping.Item1,
                        mapping.Item2,
                        StreamingAssetsPath,
                        monoscript
                    );
                    ba.ProcessBundle();
                }
            }

            PostProcessNonBundleLocations();
            SaveReferenceSchemas();
        }
        public void ProcessBuiltInBundle()
        {
            BuiltInBundleAnalyzer ba = new BuiltInBundleAnalyzer(
                unitybuiltins,
                StreamingAssetsPath
            );
            ba.ProcessBundle();
        }
        public void PostProcessNonBundleLocations()
        {
            CreateSceneGroup();

            var labels = AddressableAssetSettingsDefaultObject.Settings.GetLabels();

            var nbLocations = Locator.AllLocations
                .Where(l => l.ProviderId != typeof(AssetBundleProvider).ToString())
                .Where(l => !Regex.IsMatch(l.PrimaryKey, "^[0-9a-f]{32}$"));

            Dictionary<string, int> entryPKtoCount = new();
            foreach (var a in nbLocations)
            {
                if (!entryPKtoCount.Keys.Contains(a.PrimaryKey))
                    entryPKtoCount[a.PrimaryKey] = 0;

                entryPKtoCount[a.PrimaryKey]++;
            }

            nbLocations = nbLocations.Where(l => 
                !labels.Contains(l.PrimaryKey.ToLower()) || (
                    Path.GetFileNameWithoutExtension(l.InternalId).Equals(l.PrimaryKey) && 
                    entryPKtoCount[l.PrimaryKey] == 1
            ));

            foreach (var a in nbLocations)
            {
                RekeyLocation(a);
            }
        }

        public void CreateSceneGroup()
        {
            int counter = 0;

            var SceneGroup = CreateOrGetGroup("Scenes", BundledAssetGroupSchema.BundlePackingMode.PackSeparately);
            var sceneLocations = Locator.AllLocations
                .Where(l => l.ProviderId != typeof(AssetBundleProvider).ToString())
                .Where(l => l.ResourceType.ToString() == "UnityEngine.ResourceManagement.ResourceProviders.SceneInstance");

            SceneGroup.GetSchema<BundledAssetGroupSchema>().IncludeInBuild = false;
            SceneGroup.GetSchema<AddressableReferenceSchema>().IsEnabled = false;
            
            using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker())
            {
                foreach (var loc in sceneLocations)
                {
                    progressTracker.UpdateTask($"({++counter}/{groupMapping.Count}) - Processing Scene : {Path.GetFileName(loc.InternalId)}");
                    var assetGuid = AssetDatabase.AssetPathToGUID(loc.InternalId);
                    if (assetGuid.Equals(string.Empty))
                    {
                        Debug.LogWarning($"[AD9S Referencer] Could not find asset {loc.InternalId} for catalog entry {loc.PrimaryKey}");
                        continue;
                    }

                    AddressableAssetSettingsDefaultObject.Settings.CreateOrMoveEntry(assetGuid, SceneGroup, true, true);
                }
            }
        }
   
        public HashSet<string> DeconcatenateLabels(HashSet<string> labels)
        {
            var listLabels = labels.ToList().OrderBy(s => s.Length).ToList();
            Dictionary<string, List<string>> result = new();

            foreach (var label in listLabels)
            {
                
                int n = label.Length;

                var dp = new List<string>[n + 1];
                dp[0] = new List<string>();

                for (int i = 1; i <= n; i++)
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (dp[j] == null) continue;

                        if (j == 0 && i == n) continue;

                        string piece = label.Substring(j, i - j);
                        if (listLabels.Contains(piece))
                        {
                            dp[i] = new List<string>(dp[j]) { piece };
                            break;
                        }
                    }
                }

                result[label] = dp[n];
            }

            return result
                .Where(k => k.Value == null)
                .Select(k => k.Key)
                .ToHashSet();
        }

        public void RekeyLocation(IResourceLocation location)
        {

            var entries = AddressableAssetSettingsDefaultObject.Settings.groups
                .Where(g => g != null)
                .SelectMany(g => g.entries)
                .Where(e => e.AssetPath.Equals(location.InternalId));

            if (entries.Count() == 0)
                return;

            var entry = entries.First();

            if (entries.Count() > 1)
                Debug.LogWarning($"Encountered multiple entries when trying to re-key entry {entry.address} to {location.PrimaryKey}");

            Debug.Log($"[AD9S Referencer] Setting addressable entry address from {entry.address} to {location.PrimaryKey}");
            entry.SetAddress(location.PrimaryKey);

        }

        public static AddressableAssetGroup CreateOrGetGroup(string name, BundledAssetGroupSchema.BundlePackingMode mode)
        {
            var assetGroup = AddressableAssetSettingsDefaultObject.Settings.FindGroup(g => g.Name == $"{name} (Reference)" && g.IsReferenceGroup());

            if (assetGroup == null)
            {
                assetGroup = AddressableAssetSettingsDefaultObject.Settings.CreateGroup(
                    $"{name} (Reference)",
                    false,
                    true,
                    true,
                    new() {
                        ScriptableObject.CreateInstance<AddressableReferenceSchema>(),
                        CreateBundleSchema(
                            mode
                        ),
                    }
                );
            }

            return assetGroup;

        }
        public static BundledAssetGroupSchema CreateBundleSchema(
        BundledAssetGroupSchema.BundlePackingMode packMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether,
        BundledAssetGroupSchema.BundleNamingStyle nameStyle = BundledAssetGroupSchema.BundleNamingStyle.AppendHash)
        {

            BundledAssetGroupSchema schema = BundledAssetGroupSchema.CreateInstance<BundledAssetGroupSchema>();

            schema.InternalBundleIdMode = BundledAssetGroupSchema.BundleInternalIdMode.GroupGuid;
            schema.BundleMode = packMode;
            schema.BundleNaming = nameStyle;
            schema.IncludeGUIDInCatalog = false;
            schema.IncludeAddressInCatalog = false;
            schema.IncludeLabelsInCatalog = false;

            schema.UseAssetBundleCrc = false;
            schema.UseAssetBundleCrcForCachedBundles = false;

            schema.BuildPath.SetVariableByName(
                AddressableAssetSettingsDefaultObject.Settings,
                "Addressable References.BuildPath"
            );

            schema.LoadPath.SetVariableByName(
                AddressableAssetSettingsDefaultObject.Settings,
                "Addressable References.LoadPath"
            );

            return schema;
        }

        public void SaveReferenceSchemas()
        {
            var groups = AddressableAssetSettingsDefaultObject.Settings.groups.Where(g => g.SchemaTypes.Contains(typeof(AddressableReferenceSchema)));
            foreach (var group in groups)
            {
                AddressableReferenceSchema schema = group.Schemas.Find(s => s is AddressableReferenceSchema) as AddressableReferenceSchema;
                schema.SaveData();
            }
            EditorUtility.SetDirty(AddressableReferencerDefaultObject.Settings);
            AssetDatabase.SaveAssets();
        }
    }
}
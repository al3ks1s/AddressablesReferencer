using AddressableReferencer.Editor.Settings;
using AddressableReferencer.Editor.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace AddressableReferencer.Editor.Analyzer
{
    public class CatalogAnalyzer
    {

        public CatalogAnalyzer()
        {
            StreamingAssetsPath = AddressableReferencerDefaultObject.Settings.ExternalStreamingAssetsFolder;
            RelativeAssetsRoot = AddressableReferencerDefaultObject.Settings.RelativeAssetsRoot;
            NewTracker();
        }

        public const string AssetRoot = "Assets";
        public string StreamingAssetsPath { get; set; }
        public string RelativeAssetsRoot { get; set; }

        private ProgressTracker progressTracker;

        internal IResourceLocator Locator { get; private set; }
        AsyncOperationHandle<IResourceLocator> LocatorHandle;

        List<IResourceLocation> bundles;

        List<IResourceLocation> labelBundles;
        List<IResourceLocation> separateBundles;
        List<IResourceLocation> togetherBundles;

        IResourceLocation monoscript;
        IResourceLocation unitybuiltins;

        List<(IResourceLocation, AddressableAssetGroup)> groupMapping = new();

        List<BundleAnalyzer> analyzers = new();

        public bool LoadCatalog(string catalogPath)
        {
            UnloadCatalog();
            NewTracker();

            if (StreamingAssetsPath.Equals(string.Empty)) {
                ClearTracker();
                return false;
            }

            progressTracker.UpdateInfo($"Loading the catalog");

            Addressables.InitializeAsync().WaitForCompletion();
            LocatorHandle = Addressables.LoadContentCatalogAsync(catalogPath);
            Locator = LocatorHandle.WaitForCompletion();

            if (LocatorHandle.Status != AsyncOperationStatus.Succeeded)
            {
                UnloadCatalog();
                CleanupMemory();
                return false;
            }

            bundles = Locator.AllLocations.Where(f => f.ProviderId == typeof(AssetBundleProvider).ToString()).ToList();
            monoscript = TryFindMonoscriptBundle();
            unitybuiltins = TryFindBuiltinAssetsBundle();

            IdentifyGroups();

            ClearTracker();

            return true;
        }

        public void UnloadCatalog()
        {

            if (Locator != null)
                Addressables.RemoveResourceLocator(Locator);


            if (LocatorHandle.IsValid())
                LocatorHandle.Release();

            LocatorHandle = default;
            Locator = null;
            
        }

        public string TryFindCatalog(string catalogName = "catalog.bin")
        {

            NewTracker();

            if (!Directory.Exists(StreamingAssetsPath))
            {
                StreamingAssetsPath = string.Empty;
                ClearTracker();
                return string.Empty;
            }

            foreach (string potentialCatalog in Directory.EnumerateFiles(StreamingAssetsPath, catalogName))
            {
                var catalogPath = Path.GetDirectoryName(potentialCatalog);

                if (File.Exists(potentialCatalog))
                {

                    if (!File.Exists(Path.Join(catalogPath, "catalog.hash")))
                        Debug.LogWarning($"Catalog path was found at {potentialCatalog} but no companion hash file");

                    ClearTracker();
                    return potentialCatalog;
                }
            }
            ClearTracker();

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

            NewTracker();

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
            progressTracker.UpdateInfo($"Creating label groups");
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
        public void CreateSeparatelyPackedGroups()
        {

            progressTracker.UpdateInfo($"Creating packed separately groups");
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
        public void CreatePackedTogetherGroups()
        {

            progressTracker.UpdateInfo($"Creating packed together groups");

            foreach (var group in togetherBundles)
            {
                string groupName = group.PrimaryKey.Replace(".bundle", "").Split("_assets_").First();
                var assetGroup = CreateOrGetGroup(groupName, BundledAssetGroupSchema.BundlePackingMode.PackTogether);
                groupMapping.Add((group, assetGroup));
            }
        }

        public void ProcessGroups()
        {
            List<Task> taskList = new();
            int counter = 0;

            NewTracker(groupMapping.Count);

            var startTime = DateTime.Now;

            try {

                foreach (var mapping in groupMapping)
                {

                    if (!progressTracker.UpdateTask($"({++counter}/{groupMapping.Count}) - Processing bundle : {Path.GetFileName(mapping.Item1.InternalId)}"))
                        throw new AnalyzerProcessInterruptedException("Process was interrupted by user.");

                    BundleAnalyzer ba = new BundleAnalyzer(
                        mapping.Item1,
                        mapping.Item2,
                        StreamingAssetsPath,
                        RelativeAssetsRoot,
                        monoscript: monoscript
                    );
                    ba.progressTracker = progressTracker;
                    analyzers.Add(ba);

                    ba.ProcessBundle();
                    ba.Cleanup();

                    if (counter % 100 == 0)
                    {
                        GC.Collect();
                        // EditorUtility.UnloadUnusedAssetsImmediate();
                    }

                }

                progressTracker.UpdateTask("Post Processing");

                PostProcessNonBundleLocations();
                RefreshInternalNames();
                SaveReferenceSchemas();

            } 
            catch (AnalyzerProcessInterruptedException)
            {               
                if (EditorUtility.DisplayDialog("Process interrupted", "Analyzer process was interrupted. The bundles were only partially processed.\n\nDo you want to clear Referencer Data?", "Clear", "Ignore")){
                    var groups = AddressableAssetSettingsDefaultObject.Settings.groups.Where(g => g.SchemaTypes.Contains(typeof(AddressableReferenceSchema)));
                    foreach (var group in groups.ToArray())
                    {
                        var schema = group.GetSchema<AddressableReferenceSchema>();
                        schema.Entries.Clear();
                    }

                    AddressableReferencerDefaultObject.Settings.BuiltInBundleEntry = new();
                }
            }
            finally
            {
                var endTime = DateTime.Now;
                var elapsedSeconds = (endTime - startTime).TotalSeconds;
                Debug.Log($"Addressables Referencer analysis completed in {elapsedSeconds}s.");
                
                analyzers.Clear();

                // EditorUtility.UnloadUnusedAssetsImmediate();

                ClearTracker();
            }

            
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

            int counter = 0;
            int nblocCount = nbLocations.Count();

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

            counter = 0;

            foreach (var a in nbLocations)
            {

                if (!progressTracker.UpdateInfo($"({++counter}/{nblocCount}) - Rekeying location : {a.PrimaryKey}"))
                    throw new AnalyzerProcessInterruptedException("Process was interrupted by user.");

                RekeyLocation(a);
            }
        }
        public void CreateSceneGroup()
        {
            var SceneGroup = AddressableAssetSettingsDefaultObject.Settings.FindGroup(g => g.Name == $"Base Game Scenes");

            if (SceneGroup == null)
            {
                SceneGroup = AddressableAssetSettingsDefaultObject.Settings.CreateGroup(
                    $"Base Game Scenes",
                    false,
                    true,
                    true,
                    new() {
                        ScriptableObject.CreateInstance<AddressableReferenceSchema>(),
                        CreateBundleSchema(
                            BundledAssetGroupSchema.BundlePackingMode.PackSeparately
                        ),
                        
                    }
                );
            }

            var sceneLocations = Locator.AllLocations
                .Where(l => l.ProviderId != typeof(AssetBundleProvider).ToString())
                .Where(l => l.ResourceType.ToString() == "UnityEngine.ResourceManagement.ResourceProviders.SceneInstance");

            SceneGroup.GetSchema<AddressableReferenceSchema>().IsEnabled = false;
            SceneGroup.GetSchema<AddressableReferenceSchema>().ReferenceEnabled = false;
            SceneGroup.GetSchema<BundledAssetGroupSchema>().IncludeInBuild = false;
            SceneGroup.GetSchema<BundledAssetGroupSchema>().BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;

            int counter = 0;
            int sceneCount = sceneLocations.Count();

            foreach (var loc in sceneLocations)
            {

                var scenePath = GetAssetPath(loc.InternalId);

                if (!progressTracker.UpdateInfo($"({++counter}/{sceneCount}) - Processing Scene : {Path.GetFileName(scenePath)}")) 
                    throw new AnalyzerProcessInterruptedException("Process was interrupted by user.");

                var assetGuid = AssetDatabase.AssetPathToGUID(scenePath);
                if (assetGuid.Equals(string.Empty))
                {
                    Debug.LogWarning($"[AD9S Referencer] Could not find asset {scenePath} for catalog entry {loc.PrimaryKey}");
                    continue;
                }

                AddressableAssetSettingsDefaultObject.Settings.CreateOrMoveEntry(assetGuid, SceneGroup, true, true);
            }
        }
        public void RekeyLocation(IResourceLocation location)
        {

            var entries = AddressableAssetSettingsDefaultObject.Settings.groups
                .Where(g => g != null)
                .SelectMany(g => g.entries)
                .Where(e => e.AssetPath.Equals(GetAssetPath(location.InternalId)));

            if (entries.Count() == 0)
                return;

            var entry = entries.First();

            if (entries.Count() > 1)
                Debug.LogWarning($"Encountered multiple entries when trying to re-key entry {entry.address} to {location.PrimaryKey}");

            Debug.Log($"[AD9S Referencer] Setting addressable entry address from {entry.address} to {location.PrimaryKey}");
            entry.SetAddress(location.PrimaryKey);
            if (entry.parentGroup.HasSchema<BundledAssetGroupSchema>())
            {
                entry.parentGroup.GetSchema<BundledAssetGroupSchema>().IncludeAddressInCatalog = true;
            }
        }
        public void RefreshInternalNames()
        {
            int counter = 0;

            foreach (var analyzer in analyzers)
            {
                if (!progressTracker.UpdateInfo($"({++counter}/{analyzers.Count}) - Refreshing bundle internal name : {Path.GetFileName(analyzer.location.PrimaryKey)}"))
                    throw new AnalyzerProcessInterruptedException("Process was interrupted by user.");                
                
                analyzer.GenerateBundleName();
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


        public string GetAssetPath(string ContainerPath)
        {
            if (string.IsNullOrEmpty(ContainerPath))
            {
                return ContainerPath;
            }
            return Path.Combine(CatalogAnalyzer.AssetRoot, RelativeAssetsRoot, Path.GetRelativePath(CatalogAnalyzer.AssetRoot, ContainerPath)).Replace("\\", "/");
        }

        private void NewTracker(int TaskCount = 1)
        {
            ClearTracker();

            progressTracker = new();
            progressTracker.TaskCount = TaskCount;
        }

        private void ClearTracker()
        {
            if (progressTracker != null) { 
                progressTracker.Dispose();
                progressTracker = null;
            }
        }
    
    
        public void CleanupMemory()
        {
            // EditorUtility.UnloadUnusedAssetsImmediate();
        }
    
    }

    public class AnalyzerProcessInterruptedException : Exception
    {
        public AnalyzerProcessInterruptedException()
        {
        }

        public AnalyzerProcessInterruptedException(string message)
            : base(message)
        {
        }

        public AnalyzerProcessInterruptedException(string message, Exception inner)
            : base(message, inner)
        {
        }

    }

}
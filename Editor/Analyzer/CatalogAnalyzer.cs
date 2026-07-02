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
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

public class CatalogAnalyzer
{

    public CatalogAnalyzer(string assetPath)
    {
        StreamingAssetsPath = assetPath;
    }

    public string StreamingAssetsPath { get; set; }

    internal IResourceLocator Locator { get; private set; }
    List<IResourceLocation> bundles;

    List<IResourceLocation> labelBundles;
    List<IResourceLocation> separateBundles;

    IResourceLocation monoscript;
    IResourceLocation unitybuiltins;

    List<(IResourceLocation, AddressableAssetGroup)> groupMapping = new();


    public void LoadCatalog(string catalogPath)
    {
        using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker()) {

            progressTracker.UpdateTask($"Loading the catalog");

            Locator = Addressables.LoadContentCatalogAsync(catalogPath).WaitForCompletion();
            bundles = Locator.AllLocations.Where(f => f.ProviderId == typeof(AssetBundleProvider).ToString()).ToList();
            monoscript = bundles.Find(f => f.PrimaryKey.Contains("monoscripts"));
            unitybuiltins = bundles.Find(f => f.PrimaryKey.Contains("unitybuiltinassets"));

            IdentifyGroups();
        }
    }
    
    public void IdentifyGroups()
    {
        List<IResourceLocation> assetBundles = bundles.Where(b => b.PrimaryKey.Contains("_assets_")).ToList();

        labelBundles = assetBundles.Where(b => b.PrimaryKey.Split("/").Last().Contains("_assets_")).ToList();
        separateBundles = assetBundles.Where(b => !b.PrimaryKey.Split("/").Last().Contains("_assets_")).ToList();

        CreateLabelsAssetGroups();
        CreateSeparatelyPackedGroups();

    }

    public void SaveReferenceSchemas()
    {
        var groups = AddressableAssetSettingsDefaultObject.Settings.groups.Where(g => g.SchemaTypes.Contains(typeof(AddressableReferenceSchema)));
        foreach (var group in groups) 
        {
            AddressableReferenceSchema schema = group.Schemas.Find(s => s is AddressableReferenceSchema) as AddressableReferenceSchema;
            schema.SaveData();
        }
        
    }

    public void CreateLabelsAssetGroups()
    {
        using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker())
        {

            progressTracker.UpdateTask($"Creating label groups");
            HashSet<string> labels = new HashSet<string>();
            HashSet<string> groups = new HashSet<string>();

            labels = labelBundles.Select(b => Regex.Replace(b.PrimaryKey.Split("_assets_").Last().Replace(".bundle", ""), "_[0-9a-f]{32}", "")).ToHashSet();
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

            progressTracker.UpdateTask($"Creating folder groups");
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

        SaveReferenceSchemas();

    }

    public static AddressableAssetGroup CreateOrGetGroup(string name, BundledAssetGroupSchema.BundlePackingMode mode)
    {
        var assetGroup = AddressableAssetSettingsDefaultObject.Settings.FindGroup($"{name} (Reference)");

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


}

using AddressableReferencer.Editor.Analyzer;
using AddressableReferencer.Editor.Analyzer.AssetAnalysis;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.BuildReportVisualizer;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.U2D;
using static UnityEditor.FilePathAttribute;

namespace AddressableReferencer.Editor.Analyzer
{
    public class BundleAnalyzer
    {

        string StreamingAssetsPath;

        public IResourceLocation location;
        public AddressableAssetGroup assetGroup;
        public BundledAssetGroupSchema schema;
        public AddressableReferenceSchema referenceSchema;
        public AddressableReferenceEntry referenceEntry;

        public RentedFileArray rfa;
        public AssetsManager mgr;
        public BundleFileInstance bundle;
        public AssetsFileInstance CABFile;
        public AssetFileInfo assetBundle;

        // Monoscript bundle lookup to get the MonoBehaviour underlying types
        IResourceLocation monoscriptLocation;
        BundleFileInstance monoscriptBundle;
        public AssetsFileInstance monoscriptFile;

        List<AddressableAssetEntry> entries = new();

        private bool IsLoaded { get; set; } = false;

        private bool IsFolderBundle
        {
            get
            {

                if (!IsLoaded)
                    return false;

                return schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            }
        }

        // Temp
        public string[] Labels
        {
            get
            {
                if (schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel)
                {
                    return new string[] { Regex.Replace(location.PrimaryKey.Split("_assets_").Last().Replace(".bundle", ""), "_[0-9a-f]{32}", "") };
                }

                return null;
            }
        }

        public int AssetCount
        {
            get
            {
                if (!IsLoaded)
                    return 0;

                var bundleBase = mgr.GetBaseField(CABFile, assetBundle);
                var assetCount = bundleBase["m_Container.Array"].AsArray.size;

                return assetCount;
            }
        }

        public string ResolveBundlePath(IResourceLocation bundle)
        {
            var bundlePath = bundle.InternalId.Replace(UnityEngine.AddressableAssets.Addressables.RuntimePath, "");
            bundlePath = Path.Join(StreamingAssetsPath, bundlePath);
            bundlePath = Path.GetFullPath(bundlePath);

            return bundlePath;
        }

        public BundleAnalyzer(IResourceLocation loc, AddressableAssetGroup grp, string streamingAssetPath, IResourceLocation monoscript = null)
        {
            mgr = BundleUtils.CreateDefaultManager();

            location = loc;
            assetGroup = grp;
            StreamingAssetsPath = streamingAssetPath;

            schema = (BundledAssetGroupSchema)assetGroup.Schemas.Find(s => s is BundledAssetGroupSchema);
            referenceSchema = (AddressableReferenceSchema)assetGroup.Schemas.Find(s => s is AddressableReferenceSchema);

            if (monoscript != null)
            {
                monoscriptLocation = monoscript;
                LoadMonoscript();
            }

            LoadBundle();

            IsLoaded = true;
        }

        public void LoadBundle()
        {

            rfa = new RentedFileArray(ResolveBundlePath(location));

            bundle = mgr.LoadBundleFile(rfa.Stream, ResolveBundlePath(location));
            CABFile = mgr.LoadAssetsFileFromBundle(bundle, 0, false);

            assetBundle = CABFile.file.GetAssetsOfType(AssetClassID.AssetBundle).First();

            var bundleBase = mgr.GetBaseField(CABFile, assetBundle);

            referenceEntry = referenceSchema.Entries.Find(e => e.cabName.Equals(CABFile.name));

            if (referenceEntry == null)
            {
                referenceEntry = new();
                referenceSchema.Entries.Add(referenceEntry);
            }

            referenceEntry.cabName = CABFile.name;
            referenceEntry.baseInternalId = location.InternalId.Replace(UnityEngine.AddressableAssets.Addressables.RuntimePath, "{UnityEngine.AddressableAssets.Addressables.RuntimePath}");

        }

        public void LoadMonoscript()
        {
            monoscriptBundle = mgr.LoadBundleFile(ResolveBundlePath(monoscriptLocation));
            monoscriptFile = mgr.LoadAssetsFileFromBundle(monoscriptBundle, 0, false);
        }

        // Actual processing
        public void ProcessBundle()
        {

            if (referenceEntry.isDone)
            {
                rfa.Dispose();
                return;
            }

            var bundleBase = mgr.GetBaseField(CABFile, assetBundle);
            bool isScene = bundleBase["m_IsStreamedSceneAssetBundle"].AsBool;

            if (isScene)
            {
                return;
            }

            AnalyzeAssets();
            GenerateBundleName();

            referenceEntry.isDone = true;

            mgr.UnloadAll();
            GC.Collect();

            rfa.Dispose();

        }

        public void AnalyzeAssets()
        {
            using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker())
            {
                var bundleBase = mgr.GetBaseField(CABFile, assetBundle);

                string bundleName = Path.GetFileName(location.InternalId);
                string entryPath = String.Empty;

                int counter = 0;

                if (IsFolderBundle)
                {
                    var analyzer = new FolderAnalyzer(this);

                    var kvp = analyzer.Analyze(0, "");

                    if (kvp.Item1 != null)
                        entries.Add(kvp.Item1);

                    if (kvp.Item2 != null)
                        referenceEntry.m_ObjectMapping.AddRange(kvp.Item2);

                }
                else
                {

                    foreach (var asset in bundleBase["m_Container.Array"])
                    {
                        string path = asset["first"].AsString;
                        long pathId = asset["second.asset.m_PathID"].AsLong;
                        var assetExt = mgr.GetExtAsset(CABFile, 0, pathId, true);

                        progressTracker.UpdateInfo($"({++counter}/{AssetCount}) - Processing: {Path.GetFileName(path)}");

                        // var analyzer = GenericAnalyzer.GetAnalyzer(assetExt.baseField.TypeName, this);
                        var analyzer = GenericAnalyzer.GetAnalyzer(assetExt.info.TypeId, this);

                        var kvp = analyzer.Analyze(pathId, path);

                        if (kvp.Item1 != null)
                            entries.Add(kvp.Item1);

                        if (kvp.Item2 != null)
                        {
                            referenceEntry.m_ObjectMapping.AddRange(kvp.Item2);

                        }
                    }
                }
            }
        }

        public void GenerateBundleName()
        {

            if (entries.Count == 0) return;

            string groupHash = CalculateGroupHash(schema.InternalBundleIdMode, assetGroup, entries);
            string address = string.Empty;

            switch (schema.BundleMode)
            {
                case BundledAssetGroupSchema.BundlePackingMode.PackTogether:
                    address = $"all";
                    break;
                case BundledAssetGroupSchema.BundlePackingMode.PackSeparately:
                    address = entries[0].address;
                    break;
                case BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel:
                    var sb = new StringBuilder();
                    foreach (var l in entries[0].labels)
                        sb.Append(l);
                    address = sb.ToString();
                    break;
            }

            string bundleName = $"{groupHash}_assets_{address}.bundle";
            bundleName = bundleName.ToLower().Replace(" ", "").Replace('\\', '/').Replace("//", "/");

            string hashedBundleName = HashingMethods.Calculate(bundleName) + ".bundle";
            referenceEntry.internalName = hashedBundleName;

        }

        internal static string CalculateGroupHash(BundledAssetGroupSchema.BundleInternalIdMode mode, AddressableAssetGroup assetGroup, IEnumerable<AddressableAssetEntry> entries)
        {
            switch (mode)
            {
                case BundledAssetGroupSchema.BundleInternalIdMode.GroupGuid:
                    return assetGroup.Guid;
                case BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdHash:
                    return HashingMethods.Calculate(assetGroup.Guid, Application.cloudProjectId).ToString();
                case BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdEntriesHash:
                    return HashingMethods.Calculate(assetGroup.Guid, Application.cloudProjectId, new HashSet<string>(entries.Select(e => e.guid))).ToString();
            }

            throw new Exception("Invalid naming mode.");
        }

    }
}
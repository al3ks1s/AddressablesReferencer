using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using static UnityEngine.GraphicsBuffer;

namespace AddressableReferencer.Editor.Build.SchemaBuilders
{

    /// <summary>
    /// SchemaBuilder used to generate and export a catalog to the build path of the group being processed. 
    /// This will merge catalogs for all Addressable groups with the same build location.
    /// </summary>
    public class CatalogExportSchemaBuilder : ISchemaBuilder
    {

        public string Name => "Catalog Export";

        List<AddressableAssetGroup> m_CatalogExportGroups = new();
        Dictionary<string, HashSet<AddressableAssetGroup>> m_buildPathToGroups = new();
        Dictionary<string, HashSet<BuildTarget>> m_buildPathToTargets = new();

        List<ContentCatalogDataEntry> m_commonLocations = new();
        Dictionary<string, List<ContentCatalogDataEntry>> additionalCatalogs = new();

        
        /// <inheritdoc/>
        public bool CanBuildSchema(AddressableAssetGroupSchema schema)
        {
            return schema is ExportCatalogSchema;
        }
        
        /// <inheritdoc/>
        public Dictionary<string, List<ContentCatalogDataEntry>> GenerateCatalogLocations(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {
            if (m_buildPathToGroups.Count == 0) 
                return null;
            
            GatherCommonBundleLocations(aaContext);

            foreach (var BuildPathVar in m_buildPathToGroups.Keys)
            {
                var groups = m_buildPathToGroups[BuildPathVar];
                var buildTargets = m_buildPathToTargets[BuildPathVar];

                var buildPath = aaContext.Settings.profileSettings.GetValueByName(aaContext.Settings.activeProfileId, BuildPathVar);
                var loadPath = aaContext.Settings.profileSettings.GetValueByName(aaContext.Settings.activeProfileId, BuildPathVar.Replace("BuildPath", "LoadPath"));
                                 
                List<ContentCatalogDataEntry> entries = new List<ContentCatalogDataEntry>();
                foreach (var group in groups)
                {
                    var groupLocations = GatherAddressableGroupCatalogEntries(aaContext, group);
                    entries.AddRange(groupLocations);
                }

                foreach (var target in buildTargets)
                {
                    string catalogId = $"{BuildPathVar}-{target}";
                    var generatedCatalog = CreateCatalogForGroupsTargetPair(aaContext, entries, loadPath, target);
                    additionalCatalogs[catalogId] = generatedCatalog;
                }
            }
            return additionalCatalogs;
        }
        
        /// <summary>
        /// Function used to copy the catalogs created by this schema builder to their respective BuildPaths. 
        /// The function is repurposed to bypass the need of rewriting parts of the main build logic.
        /// </summary>
        /// <param name="aaContext"></param>
        /// <param name="addrResult"></param>
        public void GenerateContentUpdate(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult) 
        {
            if (m_buildPathToGroups.Count == 0)
                return;

            foreach (var catalog in additionalCatalogs)
            {
                string basePath = Path.Combine(Addressables.BuildPath, catalog.Key);
                string varName = catalog.Key.Split("-").First();

                string outputPath = aaContext.Settings.profileSettings.GetValueByName(aaContext.Settings.activeProfileId, varName);
                outputPath = aaContext.Settings.profileSettings.EvaluateString(aaContext.Settings.activeProfileId, outputPath);

                CopyCatalogToOutputPath(basePath, outputPath, "catalog");
            }
        }
        
        /// <inheritdoc/>
        public string ProcessGroupSchema(AddressableAssetsBuildContext aaContext, AddressableAssetGroupSchema schema)
        {
            if (!CanBuildSchema(schema))
                return string.Empty;

            var pSchema = schema as ExportCatalogSchema;
            var assetGroup = pSchema.Group;

            BundledAssetGroupSchema bundleSchema = (BundledAssetGroupSchema)assetGroup.Schemas.Find(s => s is BundledAssetGroupSchema);

            if (pSchema == null ||
                bundleSchema == null ||
                !pSchema.IsEnabled ||
                !pSchema.EnableExport ||
                !bundleSchema.IncludeInBuild ||
                !bundleSchema.IsEnabled ||
                !assetGroup.entries.Any())
                return string.Empty;

            string pathVariable = bundleSchema.BuildPath.GetName(aaContext.Settings);
            // Some variables are not supported
            if (ExportCatalogSchema.IsBuildVarExcluded(pathVariable))
            {
                Debug.LogWarning($"A catalog export was requested for group {assetGroup.Name} but its BuildPath is excluded: {pathVariable}");
                return string.Empty;
            }
            
            m_CatalogExportGroups.Add(assetGroup);

            if (!m_buildPathToGroups.TryGetValue(pathVariable, out var tGroups))
                tGroups = new();
            m_buildPathToGroups.TryAdd(pathVariable, tGroups);
            tGroups.Add(assetGroup);

            // Preemptively add all the groups with the same Build path var in case the user forgets to enable the schema on the group
            foreach (var group in aaContext.Settings.groups)
                if (group.HasSchema<BundledAssetGroupSchema>())
                    if (group.GetSchema<BundledAssetGroupSchema>().BuildPath.GetName(aaContext.Settings) == pathVariable)
                        tGroups.Add(group);


            if (!m_buildPathToTargets.TryGetValue(pathVariable, out var tTargets))
                tTargets = new();
            m_buildPathToTargets.TryAdd(pathVariable, tTargets);
            
            if (pSchema.ExportForBuildTargets) { 
                foreach (var target in pSchema.BuildTargetsForCatalog)
                   tTargets.Add(target);
            }
            else
            {
                tTargets.Add(EditorUserBuildSettings.activeBuildTarget);
            }

            return string.Empty;
        }
    
        // Location identification
        private void GatherCommonBundleLocations(AddressableAssetsBuildContext aaContext)
        {
            var commonGroups = aaContext.assetGroupToBundles.Where(kvp =>
                !kvp.Key.HasSchema<ExportCatalogSchema>() && (
                    kvp.Key.HasSchema<BundledAssetGroupSchema>() &&
                    !m_buildPathToGroups.ContainsKey(kvp.Key.GetSchema<BundledAssetGroupSchema>().BuildPath.GetName(aaContext.Settings))
                ) || kvp.Key.IsDefaultGroup()
            ).Select(kpv => kpv.Key);

            foreach (var group in commonGroups)
            {
                m_commonLocations.AddRange(GatherAddressableGroupCatalogEntries(aaContext, group));
            }
        }
        private List<ContentCatalogDataEntry> GatherAddressableGroupCatalogEntries(AddressableAssetsBuildContext aaContext, AddressableAssetGroup group)
        {
            List<ContentCatalogDataEntry> groupEntries = new List<ContentCatalogDataEntry>();
            
            bool stripHashFromBundleLocation = false;

            if (group.HasSchema<BundledAssetGroupSchema>())
                stripHashFromBundleLocation = group.GetSchema<BundledAssetGroupSchema>().BundleNaming == BundledAssetGroupSchema.BundleNamingStyle.NoHash;

            var bundleLocations = aaContext.assetGroupToBundles[group]
                .Select(b => aaContext.internalToOutputBundleName[b])
                .Select(b => aaContext.locations.Find(l =>
                        l.Keys.Select(lk => stripHashFromBundleLocation ? StripHashFromBundleLocation(lk.ToString()) : lk.ToString() ).Contains(b)
                    )
                );
            groupEntries.AddRange(bundleLocations.ToList());

            var groupAssets = aaContext.GuidToCatalogLocation.Where(kpv => GUIDBelongsToAssetGroup(kpv.Key, group)).ToList();
            foreach (var locations in groupAssets)
            {
                groupEntries.AddRange(locations.Value);
            }

            return groupEntries;
        }

        // Catalog Creation
        private List<ContentCatalogDataEntry> CreateCatalogForGroupsTargetPair(AddressableAssetsBuildContext aaContext, List<ContentCatalogDataEntry> locations, string BuildPath, BuildTarget target)
        {
            List<ContentCatalogDataEntry> newCatalog = new();

            foreach (var loc in locations)
            {
                newCatalog.Add(FormatCatalogEntryForBuildTarget(loc, target));
            }

            foreach (var loc in m_commonLocations)
            {
                if (!newCatalog.Exists(l => l.Keys.Contains(loc.Keys.First())))
                    newCatalog.Add(FormatCatalogEntryForBuildTarget(loc, target));
            }

            return newCatalog;
        }
        private ContentCatalogDataEntry FormatCatalogEntryForBuildTarget(ContentCatalogDataEntry entry, BuildTarget target)
        {

            string internalId = string.Empty;
            if (entry.Provider == "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider") {
                internalId = entry.InternalId.Replace(EditorUserBuildSettings.activeBuildTarget.ToString(), target.ToString()).Replace("\\", "/").Replace('/', IResourceLocationExtension.PathSeparatorForPlatform(target));
                
                // Path handling for windows targets, the slashes of the primary key musn't be replaced by backslashes
                if (IResourceLocationExtension.PathSeparatorForPlatform(target) == '\\')
                {
                    if (internalId.Split(new string[] { "_assets_", "_scenes_" }, StringSplitOptions.None).Length > 1) { 
                    var pkj = internalId.Split(new string[] { "_assets_", "_scenes_" }, StringSplitOptions.None).Last();

                    string bpk = pkj.Replace("\\", "/");

                    internalId = internalId.Replace(pkj, bpk);
                    }
                }
            } 
            else
            {
                internalId = entry.InternalId;
            }

            // Ensure the catalog entry is new so that it doesn't collide with other catalogs, especially internal id as it is formatted for build target.
            return new ContentCatalogDataEntry(
                entry.ResourceType,
                internalId,
                entry.Provider, 
                entry.Keys,
                entry.Dependencies,
                entry.Data
            );
        }

        // Catalog Export
        private void CopyCatalogToOutputPath(string catalogBasePath, string outputFolder, string renameCatalogBaseVar = null) 
        {

            string filename = Path.GetFileName(catalogBasePath);
            string filenamePrefix = filename.Split("-").First();

            string binFile = catalogBasePath + ".bin";
            string hashFile = catalogBasePath + ".hash";

            if (!File.Exists(binFile))
            {
                Debug.LogError($"Catalog file couldn't be found at path {binFile}");
                return;
            }

            if (!File.Exists(hashFile))
            {
                Debug.LogError($"Catalog hash file couldn't be found at path {hashFile}");
                return;
            }
            CopyFileToDestinationWithTimestampIfDifferent(binFile, renameCatalogBaseVar == null ? Path.Join(outputFolder, Path.GetFileName(binFile)) : Path.Join(outputFolder, Path.GetFileName(binFile)).Replace(filenamePrefix, renameCatalogBaseVar));
            CopyFileToDestinationWithTimestampIfDifferent(binFile, renameCatalogBaseVar == null ? Path.Join(outputFolder, Path.GetFileName(hashFile)) : Path.Join(outputFolder, Path.GetFileName(hashFile)).Replace(filenamePrefix, renameCatalogBaseVar));

        }
        static void CopyFileToDestinationWithTimestampIfDifferent(string srcPath, string destPath)
        {
            if (srcPath == destPath)
                return;

            DateTime time = File.GetLastWriteTime(srcPath);
            DateTime destTime = File.Exists(destPath) ? File.GetLastWriteTime(destPath) : new DateTime();

            if (destTime == time)
                return;

            var directory = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            else if (File.Exists(destPath))
                File.Delete(destPath);
            File.Copy(srcPath, destPath);

        }

        // Various Utilities
        static string StripHashFromBundleLocation(string hashedBundleLocation)
        {
            return hashedBundleLocation.Remove(hashedBundleLocation.LastIndexOf('_')) + ".bundle";
        }
        private bool GUIDBelongsToAssetGroup(GUID guid, AddressableAssetGroup group)
        {
            foreach (var asset in group.entries)
            {
                if (asset.guid.Equals(guid.ToString()))
                    return true;

                if (asset.IsFolder)
                    if (RecurseSubAssets(guid, asset))
                        return true;
            }

            return false;
        }
        private bool RecurseSubAssets(GUID guid, AddressableAssetEntry folderEntry)
        {
            foreach (var asset in folderEntry.SubAssets)
            {
                if (asset.guid.Equals(guid.ToString()))
                    return true;

                if (asset.IsFolder)
                    if (RecurseSubAssets(guid, asset))
                        return true;
            }

            return false;
        }



        // Stubs for Interface Compliance ---------------------------------------------------------
        public void Init(AddressableAssetsBuildContext aaContext, AddressablesDataBuilderInput builderInput, BuildContext buildContext, IDataBuilder dataBuilder) { }
        public void Build(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult) { }
        public void GenerateTypeStrippingInfo(AddressableAssetsBuildContext aaContext, ContentCatalogData contentCatalog) { }

    }
}
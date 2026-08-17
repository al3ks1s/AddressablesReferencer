using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.CatalogBuilders;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;

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

        public bool IsDataBuilt()
        {
            return true; 
        }
        /// <inheritdoc/>
        public bool CanBuildSchema(AddressableAssetGroupSchema schema)
        {
            return schema is ExportCatalogSchema;
        }

        public void Build(BuildContext buildContext, AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext, ExtractDataTask extractData, List<CachedAssetState> cachedState, AddressablesPlayerBuildResult addrResult) 
        { additionalCatalogs = GenerateCatalogLocations(aaContext, addrResult); }
        
        /// <inheritdoc/>
        public List<ContentCatalogData> GenerateCatalogs(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {
            List<ContentCatalogData> catalogs = new();
            
            foreach (var catalogId in additionalCatalogs.Keys)
            {
                catalogs.Add(GenerateContentCatalog(catalogId, additionalCatalogs[catalogId], builderInput, aaContext, addrResult));
            }

            if (catalogs.Count != 0) 
            { 
                foreach (var catalog in additionalCatalogs)
                {
                    string basePath = Path.Combine(Addressables.BuildPath, catalog.Key);
                    string varName = catalog.Key.Split("-").First();

                    string outputPath = aaContext.Settings.profileSettings.GetValueByName(aaContext.Settings.activeProfileId, varName);
                    outputPath = aaContext.Settings.profileSettings.EvaluateString(aaContext.Settings.activeProfileId, outputPath);

                    CopyCatalogToOutputPath(basePath, outputPath, "catalog");
                }
            }
            return catalogs;
        }
                
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




        /// <inheritdoc/>
        public string ProcessGroupSchema(AddressableAssetGroupSchema schema, AddressableAssetGroup assetGroup, AddressableAssetsBuildContext aaContext)
        {
            if (!CanBuildSchema(schema))
                return string.Empty;

            if (schema is ExportCatalogSchema)
                return ProcessGroupExportSchema(schema as ExportCatalogSchema, assetGroup, aaContext);

            return string.Empty;
        }
        public string ProcessGroupExportSchema(ExportCatalogSchema schema, AddressableAssetGroup assetGroup, AddressableAssetsBuildContext aaContext)
        {
            var pSchema = schema as ExportCatalogSchema;

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
            if (ExportCatalogSchema.IsBuildVarExcluded(pathVariable)) {
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

            if (pSchema.ExportForBuildTargets)
            {
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
                )
            ).Select(kpv => kpv.Key).ToList();

            commonGroups.Add(aaContext.Settings.GetSharedBundleGroup());

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
        private ContentCatalogData GenerateContentCatalog(string catalogId, List<ContentCatalogDataEntry> locations, AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {

            IBuildLogger Logger = builderInput.GetType().GetProperty(
                "Logger", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            ).GetValue(builderInput) as IBuildLogger;


            // Rest is basically copied over from BundledAssetSchemaBuilder
            var aaSettings = aaContext.Settings;
            var versionedFileName = aaSettings.profileSettings.EvaluateString(aaSettings.activeProfileId, "/catalog_" + builderInput.PlayerVersion);
            var remoteBuildPath = aaSettings.RemoteCatalogBuildPath.Id != "" ? aaSettings.RemoteCatalogBuildPath.GetValue(aaSettings) : "";
            var remoteLoadPath = aaSettings.RemoteCatalogLoadPath.Id != "" ? aaSettings.RemoteCatalogLoadPath.GetValue(aaSettings) : "";
            var catalogPathConfig = new CatalogPathConfig()
            {
                BuildPath = Addressables.BuildPath,
                RemoteBuildPath = remoteBuildPath,
                RemoteLoadPath = remoteLoadPath,
                RuntimeCatalogFilename = catalogId,
                VersionedCatalogFileName = versionedFileName,
            };

            string buildResultHash = null;
            if (addrResult != null)
            {
                object[] hashingObjects = new object[addrResult.AssetBundleBuildResults.Count];
                for (int i = 0; i < addrResult.AssetBundleBuildResults.Count; ++i)
                    hashingObjects[i] = addrResult.AssetBundleBuildResults[i].Hash;
                buildResultHash = HashingMethods.Calculate(hashingObjects).ToString();
            }

#if UNITY_6000_5_OR_NEWER
            // this variable is always reset when Init is called at the start of a build when we initialize the build context.
            m_BuiltTypeTreeDataPath = Path.Combine(Addressables.BuildPath, BuildScriptPackedMode.kTypeTreeDataFileName);
            if (aaContext.Settings.ExtractTypeTreeData)
            {
                aaContext.providerTypes.Add(typeof(CachedFileProvider));
                if (builderInput.PreviousContentState != null)
                {
                    var strippedPath = Path.GetTempFileName();
                    if (builderInput.PreviousContentState.typeTreeHashes != null)
                        ContentBuildInterface.StripTypeTreeDataFromFile(builderInput.PreviousContentState.typeTreeHashes, m_BuiltTypeTreeDataPath, strippedPath);
                    else
                        strippedPath = m_BuiltTypeTreeDataPath;

                    var hashStr = Hash128.Compute(File.ReadAllBytes(strippedPath)).ToString();
                    var newPath = $"{aaContext.Settings.RemoteCatalogBuildPath.GetValue(aaContext.Settings)}/{hashStr}{BuildScriptPackedMode.kTypeTreeDataExtension}";
                    if (!Directory.Exists(Path.GetDirectoryName(newPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                    if(File.Exists(newPath))
                        File.Delete(newPath);
                    File.Move(strippedPath, newPath);
                    builderInput.Registry.AddFile(newPath);

                    string remoteURL = $"{aaContext.Settings.RemoteCatalogLoadPath.GetValue(aaContext.Settings)}/{hashStr}{BuildScriptPackedMode.kTypeTreeDataExtension}";
                    locations.Add(new ContentCatalogDataEntry(typeof(string),
                        remoteURL,  //for remote content, the url
                        typeof(CachedFileProvider).FullName,
                        new string[] { ResourceManagerRuntimeData.kTypeTreeDataAddress },
                        null,
                        new ProviderLoadRequestOptions
                        {
                            IgnoreFailures = false,
                            LocalCachePath = $"{hashStr[0]}{hashStr[1]}/{hashStr}"
                        }));
                }
                //only add the local tt data location if this is NOT a content update OR if the baseline build has hashes (tt extraction was enabled)
                if (builderInput.PreviousContentState == null || (builderInput.PreviousContentState.typeTreeHashes != null && builderInput.PreviousContentState.typeTreeHashes.Length > 0))
                {
                    locations.Add(new ContentCatalogDataEntry(typeof(string),
                    "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/" + BuildScriptPackedMode.kTypeTreeDataFileName,
                    typeof(CachedFileProvider).FullName,
                    new string[] { ResourceManagerRuntimeData.kTypeTreeDataAddress }));
                }
            }
            else
            {
                if (File.Exists(m_BuiltTypeTreeDataPath))
                    File.Delete(m_BuiltTypeTreeDataPath);
                m_BuiltTypeTreeDataPath = string.Empty;
            }
#endif

#if ENABLE_JSON_CATALOG
            CatalogBundleConfig catalogBundleConfig = null;
            if (aaContext.Settings.BundleLocalCatalog)
            {
                var configFolder = AddressableAssetSettingsDefaultObject.kDefaultConfigFolder;
                if (builderInput.AddressableSettings != null && builderInput.AddressableSettings.IsPersisted)
                    configFolder = builderInput.AddressableSettings.ConfigFolder;

                catalogBundleConfig = new CatalogBundleConfig
                {
                    ConfigFolder = configFolder
                };
            }

            var catalogBuilder = new JsonCatalogBuilder();

            return catalogBuilder.GenerateCatalog(
                Logger,
                catalogPathConfig,
                catalogId,
                locations,
                aaContext.runtimeData.CatalogLocations,
                aaContext.providerTypes,
                builderInput.Registry,
                buildResultHash,
                aaContext.Settings.BuildRemoteCatalog,
                aaContext.Settings.CatalogRequestsTimeout
            );
#else
            var catalogBuilder = new BinaryCatalogBuilder();

            return catalogBuilder.GenerateCatalog(
                Logger,
                catalogPathConfig,
                catalogId,
                locations,
                aaContext.runtimeData.CatalogLocations,
                aaContext.providerTypes,
                builderInput.Registry,
                buildResultHash,
                aaContext.Settings.BuildRemoteCatalog,
                aaContext.Settings.CatalogRequestsTimeout
            );
#endif
        }        
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

        // Catalog Copy
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




        // Stubs for Interface compliance ---------------------------------------------------------
        /// <inheritdoc/>
        public void GenerateContentUpdate(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext, ExtractDataTask extractData, List<CachedAssetState> cachedState, AddressablesPlayerBuildResult addrResult) { }
        /// <inheritdoc/>
        public void GenerateTypeStrippingInfo(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext, ContentCatalogData contentCatalog) { }
        /// <inheritdoc/>
        public void Init(AddressableAssetsBuildContext aaContext, IDataBuilder dataBuilder) { }

    }
}
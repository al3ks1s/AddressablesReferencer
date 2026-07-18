using AddressableReferencer.Editor.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace AddressableReferencer.Editor.Build {

    /// <summary>
    /// Schema-driven build script used by <see cref="BuildScriptReferenceMode"/>. Extends the implementation of the <see cref="BuildScriptPackedMode"/> 
    /// build process to reference existing bundles.
    /// </summary>
    public class BuildScriptReferenceSchemaDriven : BuildScriptSchemaDriven
    {
        
        private Dictionary<ObjectIdentifier, long> m_objectReferences = new();
        private Dictionary<string, string> m_bundleReferences = new();
        private Dictionary<string, AddressableReferenceEntry> m_internalNameToReferenceEntry = new();

        /// <inheritdoc />
        private void AddInstanceAndSceneProvider(AddressableAssetsBuildContext aaContext)
        {
            aaContext.providerTypes.Add(instanceProviderType.Value);
            aaContext.providerTypes.Add(sceneProviderType.Value);
        }

        /// <inheritdoc />
        private TResult CreateErrorResult<TResult>(string errorString, AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext) where TResult : IDataBuilderResult
        {
            BuildLayoutGenerationTask.GenerateErrorReport(errorString, aaContext, builderInput.PreviousContentState);
            return AddressableAssetBuildResult.CreateResult<TResult>(null, 0, errorString);
        }

        /// <inheritdoc />
        protected override TResult DoBuild<TResult>(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext)
        {

            // Mostly the same code as the parent class, no need to fix something that works
       
            var genericResult = AddressableAssetBuildResult.CreateResult<TResult>();
            AddressablesPlayerBuildResult addrResult = genericResult as AddressablesPlayerBuildResult;

            ExtractDataTask extractData = new ExtractDataTask();
            List<CachedAssetState> carryOverCachedState = new List<CachedAssetState>();
            if (!BuildUtility.CheckModifiedScenesAndAskToSave())
                return CreateErrorResult<TResult>("Unsaved scenes", builderInput, aaContext);

            AddInstanceAndSceneProvider(aaContext);

            var contentCatalogs = new List<ContentCatalogData>();
            BuildContext buildContext = new BuildContext(aaContext, Log, new ReferenceIdentifier(m_bundleReferences, m_objectReferences, aaContext.Settings.ContiguousBundles));
            foreach (ISchemaBuilder schemaBuilder in SchemaBuilders)
            {
                schemaBuilder.Build(
                    buildContext,
                    builderInput,
                    aaContext,
                    extractData,
                    carryOverCachedState,
                    addrResult);


                var targets = AddressableReferencerDefaultObject.Settings.BuildTargetsForCatalog;
                targets.Add(EditorUserBuildSettings.activeBuildTarget);
                
                foreach (var target in targets)
                {
                    SwapOutLocationsForTarget(aaContext, addrResult, target);

                    // Pre process the build result to edit the catalog using the reference locations
                    var schemaGeneratedCatalogs = schemaBuilder.GenerateCatalogs(builderInput, aaContext, addrResult);

                    foreach (var contentCatalog in schemaGeneratedCatalogs)
                    {
                        if (contentCatalog == null)
                        {
                            Debug.Log($"No catalog generated for schema builder: {schemaBuilder.Name}");
                            continue;
                        }
                        schemaBuilder.GenerateTypeStrippingInfo(builderInput, aaContext, contentCatalog);
                        schemaBuilder.GenerateContentUpdate(builderInput, aaContext, extractData, carryOverCachedState, addrResult);
                        contentCatalogs.Add(contentCatalog);

                        if (AddressableReferencerDefaultObject.Settings.MoveCatalogToSharedBundleBuildPath)
                        {
                            CopyCatalog(aaContext, contentCatalog, builderInput, target);
                        }
    
                    }
                }
            }

            // sort catalogs to be deterministic
            aaContext.runtimeData.CatalogLocations.Sort((a, b) => string.Compare(a.InternalId, b.InternalId, StringComparison.Ordinal));
            var settingsPath = GenerateRuntimeSettingsFile(aaContext, builderInput);
            genericResult.LocationCount = aaContext.locations.Count;
            genericResult.OutputPath = settingsPath;

            GenerateBuildLayout(extractData.BuildContext, aaContext.internalToOutputBundleName, contentCatalogs.ToArray(), addrResult);
            return genericResult;
    
        }

        /// <inheritdoc />
        private void GenerateBuildLayout(IBuildContext buildContext,
        Dictionary<string, string> bundleRenameMap,
        ContentCatalogData[] contentCatalogs,
        AddressablesPlayerBuildResult buildResult)
        {
            if (ProjectConfigData.GenerateBuildLayout && buildContext != null)
            {
                using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker())
                {
                    progressTracker.UpdateTask("Generating Build Layout");
                    using (Log.ScopedStep(LogLevel.Info, "Generate Build Layout"))
                    {
                        List<IBuildTask> tasks = new List<IBuildTask>();
                        var buildLayoutTask = new BuildLayoutGenerationTask();
                        buildContext.SetContextObject<IBuildLayoutParameters>(new BuildLayoutParameters(bundleRenameMap, contentCatalogs, buildResult));
                        tasks.Add(buildLayoutTask);
                        BuildTasksRunner.Run(tasks, buildContext);
                    }
                }
            }
        }

        /// <inheritdoc />
        protected override string ProcessGroupSchema(AddressableAssetGroupSchema schema, AddressableAssetGroup assetGroup, AddressableAssetsBuildContext aaContext)
        {

            // Debug.Log($"Processing schema of {assetGroup.Name}");

            if (schema is AddressableReferenceSchema)
            {
                ProcessReferenceSchema(schema as AddressableReferenceSchema, assetGroup, aaContext);
            } 
            else 
            { 

                foreach (var schemaBuilder in SchemaBuilders)
                {
                    if (!schemaBuilder.CanBuildSchema(schema))
                        continue;
                    var errorString = schemaBuilder.ProcessGroupSchema(schema, assetGroup, aaContext);
                    if (errorString != string.Empty)
                        return errorString;
                }
            } 
            AssetDatabase.Refresh();
            return string.Empty;
        }
        private string ProcessReferenceSchema(AddressableReferenceSchema schema, AddressableAssetGroup assetGroup, AddressableAssetsBuildContext aaContext)
        {

            BundledAssetGroupSchema bundleSchema = (BundledAssetGroupSchema)assetGroup.Schemas.Find(s => s is BundledAssetGroupSchema);
        
            if (schema == null ||
                bundleSchema == null ||
                !schema.IsEnabled ||
                !schema.ReferenceEnabled ||
                !bundleSchema.IncludeInBuild || 
                !bundleSchema.IsEnabled || 
                !assetGroup.entries.Any())
                return string.Empty;
        
            // Debug.Log($"Processing schema for {assetGroup.Name}, has {schema.Entries.Count} entries");

            foreach (var entry in schema.Entries)
            {

                // Debug.Log($"Entry has {entry.ObjectMappingDict.Count} objects, Adding {entry.internalName} - {entry.cabName}");
                m_bundleReferences.TryAdd(entry.internalName, entry.cabName);
                m_internalNameToReferenceEntry.TryAdd(entry.internalName, entry);

                foreach (var map in entry.ObjectMappingDict)
                {
                    m_objectReferences.TryAdd(map.Key, map.Value);
                }
            }

            return string.Empty;

        }
        
        private void SwapOutLocationsForTarget(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult, BuildTarget target = BuildTarget.NoTarget)
        {
            foreach (var internalBundleName in aaContext.internalToOutputBundleName)
            {

                ContentCatalogDataEntry catalogEntry = aaContext.locations.Find(l => l.Keys[0].Equals(internalBundleName.Value));

                if (catalogEntry != null)
                {
                    if (m_internalNameToReferenceEntry.TryGetValue(internalBundleName.Key, out AddressableReferenceEntry baseLocation))
                    {
                        catalogEntry.InternalId = FormatBaseLocationForTarget(baseLocation, target);
                    }
                }
            }
        }
        private string FormatBaseLocationForTarget(AddressableReferenceEntry baseLocation, BuildTarget target)
        {

            string internalId = baseLocation.baseInternalId.Replace("{BuildTarget}", Enum.GetName(typeof(BuildTarget), target)).Replace('/', IResourceLocationExtension.PathSeparatorForPlatform(target));

            // Path handling for windows targets, the slashes of the primary key musn't be replaced by backslashes
            if (IResourceLocationExtension.PathSeparatorForPlatform(target) == '\\') 
            {
                string pk = Regex.Replace(baseLocation.primaryKey, "_?[0-9a-f]{32}.bundle", "");
                string bpk = pk.Replace("/", "\\");

                internalId = internalId.Replace(bpk, pk);
            }

            return internalId;
        }

        private void CopyCatalog(AddressableAssetsBuildContext aaContext, ContentCatalogData catalogLocation, AddressablesDataBuilderInput builderInput, BuildTarget target = BuildTarget.NoTarget)
        {
            string catalogPath = Path.GetFullPath(Path.Combine(Addressables.BuildPath, builderInput.RuntimeCatalogFilename));

            var sharedBundleGroup = aaContext.Settings.GetSharedBundleGroup();
            var ContentPackingSettings = sharedBundleGroup.GetSchema<BundledAssetGroupSchema>();
            var outputPath = Path.GetFullPath(Path.Join(ContentPackingSettings.BuildPath.GetValue(aaContext.Settings, false), builderInput.RuntimeCatalogFilename));

            if (ContentPackingSettings.BuildPath.GetName(aaContext.Settings).Equals("Local.BuildPath") ) //&& target == BuildTarget.NoTarget)
                return;

            if (File.Exists(catalogPath + ".bin"))
            {
                CopyFileToDestinationWithTimestampIfDifferent(catalogPath + ".bin", outputPath + $"-{(target == BuildTarget.NoTarget ? string.Empty : Enum.GetName(typeof(BuildTarget), target))}" + ".bin");

                if (File.Exists(catalogPath + ".hash")) 
                {
                    CopyFileToDestinationWithTimestampIfDifferent(catalogPath + ".hash", outputPath + $"-{(target == BuildTarget.NoTarget ? string.Empty : Enum.GetName(typeof(BuildTarget), target))}" + ".hash");
                } 
                else
                { 
                    Debug.LogWarning($"Catalog hash file couldn't be found at path {catalogPath}.hash"); 
                }
            } 
            else
            {
                Debug.LogError($"Catalog file couldn't be found at path {catalogPath}.bin");
            }
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
    }

}
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

public class BuildScriptReferenceSchemaDriven : BuildScriptSchemaDriven
{

    private Dictionary<(GUID, long, FileType, string), long> m_objectReferences = new();
    private Dictionary<string, string> m_bundleReferences = new();
    private Dictionary<string, string> m_internalNameToBaseInternalId = new();

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

        Debug.Log($"Built using reference mode");
        
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

            SwapOutLocations(aaContext, addrResult);

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
            m_internalNameToBaseInternalId.TryAdd(entry.internalName, entry.baseInternalId);

            foreach (var map in entry.ObjectMappingDict)
            {
                m_objectReferences.TryAdd(map.Key, map.Value);
            }
        }

        return string.Empty;

    }

    private void SwapOutLocations(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
    {
        foreach (var internalBundleName in aaContext.internalToOutputBundleName)
        {
            ContentCatalogDataEntry catalogEntry = aaContext.locations.Find(l => l.Keys[0].Equals(internalBundleName.Value));
            // Debug.Log($"{internalBundleName.Key}, {internalBundleName.Value}");

            if (catalogEntry != null)
            {
                // Debug.Log($"Generated Location {catalogEntry.Keys[0]}, {catalogEntry.InternalId} {catalogEntry.Dependencies.Count}");
                if (m_internalNameToBaseInternalId.TryGetValue(internalBundleName.Key, out string baseLocationInternalId))
                {
                    // Debug.Log($"Base Location {baseLocationInternalId}");
                    catalogEntry.InternalId = baseLocationInternalId;
                }
            }
        }
    }

}

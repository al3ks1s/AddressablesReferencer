using AddressableReferencer.Editor.Build.SchemaBuilders;
using AddressableReferencer.Editor.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.CatalogBuilders;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Build.DataBuilders.SchemaBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;

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

        // ------------------------------------------------------------------------------------------------
        // Please make these at least protected, i want to use them without copy pasting


        // End of private methods directly taken from Addressables to make this work
        // ------------------------------------------------------------------------------------------------

        /// <inheritdoc />
        public override ISchemaBuilder[] CreateSchemaBuilders()
        {
            return new ISchemaBuilder[] {
                new ReferenceSchemaBuilder(),
                new BundledAssetSchemaBuilder(),
                new CatalogExportSchemaBuilder(),
#if ENABLE_CONTENT_DIRECTORIES
                new ContentDirectorySchemaBuilder(),
#endif
            };
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
                    var errorString = schemaBuilder.ProcessGroupSchema(aaContext, schema);
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
        
            Debug.Log($"Processing schema for {assetGroup.Name}, has {schema.Entries.Count} entries");

            foreach (var entry in schema.Entries)
            {
                Debug.Log($"Entry has {entry.ObjectMappingDict.Count} objects, Adding {entry.internalName} - {entry.cabName}");
                m_bundleReferences.TryAdd(entry.internalName, entry.cabName);
                m_internalNameToReferenceEntry.TryAdd(entry.internalName, entry);

                foreach (var map in entry.ObjectMappingDict)
                {
                    m_objectReferences.TryAdd(map.Key, map.Value);
                }
            }

            return string.Empty;

        }
        private void ProcessBuiltInBundle(AddressableAssetsBuildContext aaContext)
        {
            if (!AddressableReferencerDefaultObject.Settings.UseBaseGameBuiltinAssets)
                return;

            var entry = AddressableReferencerDefaultObject.Settings.BuiltInBundleEntry;
            entry.internalName = GetBuiltInBundleNamePrefix(aaContext) + $"{BuildScriptBase.BuiltInBundleBaseName}.bundle";
            AddressableReferencerDefaultObject.Settings.Save();

            m_bundleReferences.TryAdd(entry.internalName, entry.cabName);

            foreach (var map in entry.ObjectMappingDict)
            {
                m_objectReferences.TryAdd(map.Key, map.Value);
            }
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

            if (AddressableReferencerDefaultObject.Settings.UseBaseGameBuiltinAssets)
            {
                var bundleResult = addrResult.AssetBundleBuildResults.Find(br => Regex.IsMatch(br.InternalBundleName, "[0-9a-f]{32}_unitybuiltinasset"));
                string bundleFileName = Path.GetFileName(bundleResult.FilePath).Replace(".bundle", "");
                var builtinsLocation = aaContext.locations.Find(l => l.Keys[0].ToString().Contains(bundleFileName));

                if (builtinsLocation != null)
                {
                    builtinsLocation.InternalId = FormatBaseLocationForTarget(AddressableReferencerDefaultObject.Settings.BuiltInBundleEntry, target);
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
            var outputPath = Path.GetFullPath(Path.Join(ContentPackingSettings.BuildPath.GetValue(aaContext.Settings, true), builderInput.RuntimeCatalogFilename));

            if (ContentPackingSettings.BuildPath.GetName(aaContext.Settings).Equals("Local.BuildPath") ) //&& target == BuildTarget.NoTarget)
                return;

            if (File.Exists(catalogPath + ".bin"))
            {
                CopyFileToDestinationWithTimestampIfDifferent(catalogPath + ".bin", outputPath + $"{(target == BuildTarget.NoTarget ? string.Empty : $"-{Enum.GetName(typeof(BuildTarget), target)}")}" + ".bin");

                if (File.Exists(catalogPath + ".hash")) 
                {
                    CopyFileToDestinationWithTimestampIfDifferent(catalogPath + ".hash", outputPath + $"{(target == BuildTarget.NoTarget ? string.Empty : $"-{Enum.GetName(typeof(BuildTarget), target)}")}" + ".hash");
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

        private void RemoveBuiltInAssetBundle(AddressablesPlayerBuildResult addrResult)
        {
            var bundleResult = addrResult.AssetBundleBuildResults.Find(br => Regex.IsMatch(br.InternalBundleName, "[0-9a-f]{32}_unitybuiltinasset"));

            Debug.Log(bundleResult.FilePath.ToString());

            if (File.Exists(bundleResult.FilePath))
            {
                File.Delete(bundleResult.FilePath);
            }
                
        }

        internal static string GetBuiltInBundleNamePrefix(AddressableAssetsBuildContext aaContext)
        {
            return GetBuiltInBundleNamePrefix(aaContext.Settings);
        }
        internal static string GetBuiltInBundleNamePrefix(AddressableAssetSettings settings)
        {
            string value = "";
            switch (settings.BuiltInBundleNaming)
            {
                case BuiltInBundleNaming.DefaultGroupGuid:
                    value = settings.DefaultGroup.Guid;
                    break;
                case BuiltInBundleNaming.ProjectName:
                    value = UnityEngine.Hash128.Compute(GetProjectName()).ToString();
                    break;
                case BuiltInBundleNaming.Custom:
                    value = settings.BuiltInBundleCustomNaming;
                    break;
            }

            return value;
        }
        internal static string GetProjectName()
        {
            return new DirectoryInfo(Path.GetDirectoryName(Application.dataPath)).Name;
        }

    }

}
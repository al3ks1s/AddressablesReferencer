using AddressableReferencer.Editor.Settings;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace AddressableReferencer.Editor.Build.SchemaBuilders
{
    /// <summary>
    /// Preprocessor Schema builder that will gather the Addressables references and provide it to the build pipeline through a <see cref="ReferenceIdentifier"/>.
    /// 
    /// </summary>
    public class ReferenceSchemaBuilder : ISchemaBuilder
    {
        public string Name => "References";

        BuildContext m_buildContext;
        ReferenceIdentifier m_referenceIdentifier;

        private Dictionary<ObjectIdentifier, long> m_objectReferences = new();
        private Dictionary<string, string> m_bundleReferences = new();
        private Dictionary<string, AddressableReferenceEntry> m_internalNameToReferenceEntry = new();

        /// <inheritdoc/>
        public void Build(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {

            // Replace reference locations internal ids by the one in the base game catalog. Keep the {BuildTarget} unformatted for now.
            foreach (var internalBundleName in aaContext.internalToOutputBundleName)
            {

                ContentCatalogDataEntry catalogEntry = aaContext.locations.Find(l => l.Keys[0].Equals(internalBundleName.Value));

                if (catalogEntry != null)
                {
                    if (m_internalNameToReferenceEntry.TryGetValue(internalBundleName.Key, out AddressableReferenceEntry baseLocation))
                    {
                        catalogEntry.InternalId = baseLocation.baseInternalId.Replace("{BuildTarget}", EditorUserBuildSettings.activeBuildTarget.ToString());
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
                    builtinsLocation.InternalId = AddressableReferencerDefaultObject.Settings.BuiltInBundleEntry.baseInternalId.Replace("{BuildTarget}", EditorUserBuildSettings.activeBuildTarget.ToString());
                }
            }
        }

        /// <inheritdoc/>
        public bool CanBuildSchema(AddressableAssetGroupSchema schema)
        {
            return schema is AddressableReferenceSchema;
        }

        /// <inheritdoc/>
        public Dictionary<string, List<ContentCatalogDataEntry>> GenerateCatalogLocations(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {
            return null;
        }

        /// <inheritdoc/>
        public void GenerateContentUpdate(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {}

        /// <inheritdoc/>
        public void GenerateTypeStrippingInfo(AddressableAssetsBuildContext aaContext, ContentCatalogData contentCatalog)
        {}

        /// <inheritdoc/>
        public void Init(AddressableAssetsBuildContext aaContext, AddressablesDataBuilderInput builderInput, BuildContext buildContext, IDataBuilder dataBuilder)
        {
            m_buildContext = buildContext;

            if (!m_buildContext.ContainsContextObject<IDeterministicIdentifiers>()) {
                m_referenceIdentifier = new ReferenceIdentifier(m_bundleReferences, m_objectReferences, aaContext.Settings.ContiguousBundles);
                m_buildContext.SetContextObject<IDeterministicIdentifiers>(m_referenceIdentifier);
            }

            ProcessBuiltInBundle(aaContext);
        }

        /// <inheritdoc/>
        public string ProcessGroupSchema(AddressableAssetsBuildContext aaContext, AddressableAssetGroupSchema schema)
        {
            if (!CanBuildSchema(schema))
                return string.Empty;

            var pSchema = schema as AddressableReferenceSchema;
            var assetGroup = pSchema.Group;

            BundledAssetGroupSchema bundleSchema = (BundledAssetGroupSchema)assetGroup.Schemas.Find(s => s is BundledAssetGroupSchema);

            if (pSchema == null ||
                bundleSchema == null ||
                !pSchema.IsEnabled ||
                !pSchema.ReferenceEnabled ||
                !bundleSchema.IncludeInBuild ||
                !bundleSchema.IsEnabled ||
                !assetGroup.entries.Any())
                return string.Empty;

            // Debug.Log($"Processing schema for {assetGroup.Name}, has {pSchema.Entries.Count} entries");

            foreach (var entry in pSchema.Entries)
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
        private void ProcessBuiltInBundle(AddressableAssetsBuildContext aaContext)
        {
            if (!AddressableReferencerDefaultObject.Settings.UseBaseGameBuiltinAssets)
                return;

            var entry = AddressableReferencerDefaultObject.Settings.BuiltInBundleEntry;
            entry.internalName = IResourceLocationExtension.GetBuiltInBundleNamePrefix(aaContext) + $"{BuildScriptBase.BuiltInBundleBaseName}.bundle";
            AddressableReferencerDefaultObject.Settings.Save();

            m_bundleReferences.TryAdd(entry.internalName, entry.cabName);

            foreach (var map in entry.ObjectMappingDict)
            {
                m_objectReferences.TryAdd(map.Key, map.Value);
            }
        }
    }
}
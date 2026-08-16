using AddressableReferencer.Editor.Settings;
using AddressableReferencer.Editor.Utilities;
using System.Collections.Generic;
using System.Data.Common;
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
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using static UnityEngine.EventSystems.EventTrigger;
using static UnityEngine.GraphicsBuffer;

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

        /// <summary>
        /// Repurposes the method to swap out the locations of reference bundles to the ones coming from the base game.
        /// </summary>
        /// <param name="aaContext"></param>
        /// <param name="addrResult"></param>
        public void Build(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {}
        
        /// <inheritdoc/>
        public bool CanBuildSchema(AddressableAssetGroupSchema schema)
        {
            return schema is AddressableReferenceSchema;
        }
        
        /// <inheritdoc/>
        public Dictionary<string, List<ContentCatalogDataEntry>> GenerateCatalogLocations(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult) { return null; }
        
        /// <inheritdoc/>
        public void GenerateContentUpdate(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult) {}
        
        /// <inheritdoc/>
        public void GenerateTypeStrippingInfo(AddressableAssetsBuildContext aaContext, ContentCatalogData contentCatalog) {}
        
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

            foreach (var entry in pSchema.Entries)
            {
                m_bundleReferences.TryAdd(entry.internalName, entry.cabName);

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
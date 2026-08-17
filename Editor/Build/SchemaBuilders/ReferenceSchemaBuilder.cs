using AddressableReferencer.Editor.Settings;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
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
    /// </summary>
    public class ReferenceSchemaBuilder : ISchemaBuilder
    {
        public string Name => "References";

        BuildContext m_buildContext;
        ReferenceIdentifier m_referenceIdentifier;

        private Dictionary<ObjectIdentifier, long> m_objectReferences = new();
        private Dictionary<string, string> m_bundleReferences = new();
        
        /// <inheritdoc/>
        public bool CanBuildSchema(AddressableAssetGroupSchema schema)
        {
            return schema is AddressableReferenceSchema;
        }
        public bool IsDataBuilt()
        {
            return true;
        }
        
        /// <summary>
        /// Repurposes the method to provide the build context the references to base bundles.
        /// </summary>
        /// <param name="aaContext"></param>
        /// <param name="addrResult"></param>
        public void Build(BuildContext buildContext,
            AddressablesDataBuilderInput builderInput,
            AddressableAssetsBuildContext aaContext,
            ExtractDataTask extractData,
            List<CachedAssetState> cachedState,
            AddressablesPlayerBuildResult addrResult)
        {
            m_buildContext = buildContext;

            if (!m_buildContext.ContainsContextObject<IDeterministicIdentifiers>())
            {
                m_referenceIdentifier = new ReferenceIdentifier(m_bundleReferences, m_objectReferences, aaContext.Settings.ContiguousBundles);
                m_buildContext.SetContextObject<IDeterministicIdentifiers>(m_referenceIdentifier);
            }
        }
      
        /// <inheritdoc/>
        public void Init(AddressableAssetsBuildContext aaContext, IDataBuilder dataBuilder)
        {
            ProcessBuiltInBundle(aaContext);
        }
        
        /// <inheritdoc/>
        public string ProcessGroupSchema(AddressableAssetGroupSchema schema, AddressableAssetGroup assetGroup, AddressableAssetsBuildContext aaContext)
        {
            if (!CanBuildSchema(schema))
                return string.Empty;

            var pSchema = schema as AddressableReferenceSchema;

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



        // Stubs for Interface compliance ---------------------------------------------------------
        /// <inheritdoc/>
        public List<ContentCatalogData> GenerateCatalogs(AddressablesDataBuilderInput builderInput,
            AddressableAssetsBuildContext aaContext,
            AddressablesPlayerBuildResult addrResult)
        { return new(); } // Empty list 
        /// <inheritdoc/>
        public void GenerateContentUpdate(AddressablesDataBuilderInput builderInput,
            AddressableAssetsBuildContext aaContext,
            ExtractDataTask extractData,
            List<CachedAssetState> cachedState,
            AddressablesPlayerBuildResult addrResult)
        { }
        /// <inheritdoc/>
        public void GenerateTypeStrippingInfo(AddressablesDataBuilderInput builderInput,
            AddressableAssetsBuildContext aaContext,
            ContentCatalogData contentCatalog)
        { }

    }
}
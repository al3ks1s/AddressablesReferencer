using System.Collections.Generic;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Build.DataBuilders.SchemaBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.AddressableAssets.Initialization;
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

        public void Build(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {
            // Do nothing?
        }

        /// <inheritdoc/>
        public bool CanBuildSchema(AddressableAssetGroupSchema schema)
        {
            return schema is AddressableReferenceSchema;
        }

        public Dictionary<string, List<ContentCatalogDataEntry>> GenerateCatalogLocations(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {
            // Only pre-processing, return an empty dict
            return new Dictionary<string, List<ContentCatalogDataEntry>>
            {};
        }

        public void GenerateContentUpdate(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {}

        public void GenerateTypeStrippingInfo(AddressableAssetsBuildContext aaContext, ContentCatalogData contentCatalog)
        {}

        public void Init(AddressableAssetsBuildContext aaContext, AddressablesDataBuilderInput builderInput, BuildContext buildContext, IDataBuilder dataBuilder)
        {
            throw new System.NotImplementedException();
        }

        public string ProcessGroupSchema(AddressableAssetsBuildContext aaContext, AddressableAssetGroupSchema schema)
        {
            if (!CanBuildSchema(schema))
                return string.Empty;

            var pSchema = schema as AddressableReferenceSchema;



            return string.Empty;
        }
    }
}
using System.Collections.Generic;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline;
using UnityEngine;
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

        public void Build(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public bool CanBuildSchema(AddressableAssetGroupSchema schema)
        {
            return schema is ExportCatalogSchema;
        }

        public Dictionary<string, List<ContentCatalogDataEntry>> GenerateCatalogLocations(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {
            throw new System.NotImplementedException();
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

            var pSchema = schema as ExportCatalogSchema;

            return string.Empty;
        }
    }
}
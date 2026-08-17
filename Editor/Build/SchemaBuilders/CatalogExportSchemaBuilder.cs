using AddressableReferencer.Editor.Settings;
using AddressableReferencer.Editor.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        public void Build(BuildContext buildContext, AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext, ExtractDataTask extractData, List<CachedAssetState> cachedState, AddressablesPlayerBuildResult addrResult)
        {
            throw new NotImplementedException();
        }

        public bool CanBuildSchema(AddressableAssetGroupSchema schema)
        {
            throw new NotImplementedException();
        }

        public List<ContentCatalogData> GenerateCatalogs(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {
            throw new NotImplementedException();
        }

        public void GenerateContentUpdate(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext, ExtractDataTask extractData, List<CachedAssetState> cachedState, AddressablesPlayerBuildResult addrResult)
        {
            throw new NotImplementedException();
        }

        public void GenerateTypeStrippingInfo(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext, ContentCatalogData contentCatalog)
        {
            throw new NotImplementedException();
        }

        public void Init(AddressableAssetsBuildContext aaContext, IDataBuilder dataBuilder)
        {
            throw new NotImplementedException();
        }

        public bool IsDataBuilt()
        {
            throw new NotImplementedException();
        }

        public string ProcessGroupSchema(AddressableAssetGroupSchema schema, AddressableAssetGroup assetGroup, AddressableAssetsBuildContext aaContext)
        {
            throw new NotImplementedException();
        }
    }
}
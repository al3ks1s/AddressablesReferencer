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

        public void Build(BuildContext buildContext, AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext, ExtractDataTask extractData, List<CachedAssetState> cachedState, AddressablesPlayerBuildResult addrResult)
        {
            throw new System.NotImplementedException();
        }

        public bool CanBuildSchema(AddressableAssetGroupSchema schema)
        {
            throw new System.NotImplementedException();
        }

        public List<ContentCatalogData> GenerateCatalogs(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {
            throw new System.NotImplementedException();
        }

        public void GenerateContentUpdate(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext, ExtractDataTask extractData, List<CachedAssetState> cachedState, AddressablesPlayerBuildResult addrResult)
        {
            throw new System.NotImplementedException();
        }

        public void GenerateTypeStrippingInfo(AddressablesDataBuilderInput builderInput, AddressableAssetsBuildContext aaContext, ContentCatalogData contentCatalog)
        {
            throw new System.NotImplementedException();
        }

        public void Init(AddressableAssetsBuildContext aaContext, IDataBuilder dataBuilder)
        {
            throw new System.NotImplementedException();
        }

        public bool IsDataBuilt()
        {
            throw new System.NotImplementedException();
        }

        public string ProcessGroupSchema(AddressableAssetGroupSchema schema, AddressableAssetGroup assetGroup, AddressableAssetsBuildContext aaContext)
        {
            throw new System.NotImplementedException();
        }
    }
}
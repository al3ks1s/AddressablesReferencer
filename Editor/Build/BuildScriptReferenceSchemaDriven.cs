using AddressableReferencer.Editor.Build.SchemaBuilders;
using System;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Build.DataBuilders.SchemaBuilders;

namespace AddressableReferencer.Editor.Build {

    /// <summary>
    /// Schema-driven build script used by <see cref="BuildScriptReferenceMode"/>. Extends the implementation of the <see cref="BuildScriptPackedMode"/> 
    /// build process to reference existing bundles.
    /// </summary>
    public class BuildScriptReferenceSchemaDriven : BuildScriptSchemaDriven
    {

        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
        {
            throw new NotImplementedException("Building with references isn't supported on this branch of Addressables Referencer, please use either Addressables-3 or Addressables-4, look at the documentation to select a branch.");
        }

        /// <inheritdoc />
        public override ISchemaBuilder[] CreateSchemaBuilders()
        {
            return new ISchemaBuilder[] {
                new ReferenceSchemaBuilder(),
                new BundledAssetSchemaBuilder(),
#if ENABLE_CONTENT_DIRECTORIES
                new ContentDirectorySchemaBuilder(),
#endif
                new SwapInternalIdSchemaBuilder(),
                new CatalogExportSchemaBuilder(),
            };
        }
    }
}
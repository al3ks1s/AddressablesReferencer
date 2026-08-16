using AddressableReferencer.Editor.Build.SchemaBuilders;
using AddressableReferencer.Editor.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Build.DataBuilders.SchemaBuilders;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace AddressableReferencer.Editor.Build {

    /// <summary>
    /// Schema-driven build script used by <see cref="BuildScriptReferenceMode"/>. Extends the implementation of the <see cref="BuildScriptPackedMode"/> 
    /// build process to reference existing bundles.
    /// </summary>
    public class BuildScriptReferenceSchemaDriven : BuildScriptSchemaDriven
    {

        /// <inheritdoc />
        public override ISchemaBuilder[] CreateSchemaBuilders()
        {
            return new ISchemaBuilder[] {
                new BundledAssetSchemaBuilder(),
                new ReferenceSchemaBuilder(),
                new CatalogExportSchemaBuilder(),
#if ENABLE_CONTENT_DIRECTORIES
                new ContentDirectorySchemaBuilder(),
#endif
            };
        }
    }
}
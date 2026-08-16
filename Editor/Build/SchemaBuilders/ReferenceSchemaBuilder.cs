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
        private Dictionary<string, AddressableReferenceEntry> m_internalNameToReferenceEntry = new();

        /// <summary>
        /// Repurposes the method to swap out the locations of reference bundles to the ones coming from the base game.
        /// </summary>
        /// <param name="aaContext"></param>
        /// <param name="addrResult"></param>
        public void Build(AddressableAssetsBuildContext aaContext, AddressablesPlayerBuildResult addrResult)
        {

            var referenceGroups = aaContext.Settings.groups.Where(g => g.IsReferenceGroup());
            List<(ContentCatalogDataEntry, AddressableReferenceEntry)> referenceLocations = new();

            foreach (var group in referenceGroups)
            {
                bool stripHashFromBundleLocation = false;
                if (group.HasSchema<BundledAssetGroupSchema>())
                    stripHashFromBundleLocation = group.GetSchema<BundledAssetGroupSchema>().BundleNaming == BundledAssetGroupSchema.BundleNamingStyle.NoHash;

                foreach (var bundle in aaContext.assetGroupToBundles[group])
                {
                    var outputBundleName = aaContext.internalToOutputBundleName[bundle];
                    var location = aaContext.locations.Find(l => l.Keys.Select(lk => stripHashFromBundleLocation ? StripHashFromBundleLocation(lk.ToString()) : lk.ToString()).Contains(outputBundleName));

                    m_internalNameToReferenceEntry.TryGetValue(bundle, out var referenceEntry);

                    referenceLocations.Add((location, referenceEntry));
                }
            }

            foreach (var entryPair in referenceLocations) 
            {
                var catalogLocation = entryPair.Item1;
                var baseLocation = entryPair.Item2;

                FormatLocationFromReferenceEntry(catalogLocation, baseLocation);
            }

            if (AddressableReferencerDefaultObject.Settings.UseBaseGameBuiltinAssets)
            {
                bool stripHashFromBundleLocation = false;
                if (aaContext.Settings.DefaultGroup.HasSchema<BundledAssetGroupSchema>())
                    stripHashFromBundleLocation = aaContext.Settings.DefaultGroup.GetSchema<BundledAssetGroupSchema>().BundleNaming == BundledAssetGroupSchema.BundleNamingStyle.NoHash;

                if (aaContext.internalToOutputBundleName.TryGetValue(IResourceLocationExtension.GetBuiltInBundleName(aaContext), out var outputBundleName))
                {
                    var builtinLocation = aaContext.locations.Find(l => l.Keys.Select(lk => stripHashFromBundleLocation ? StripHashFromBundleLocation(lk.ToString()) : lk.ToString()).Contains(outputBundleName));
                    Debug.Log(builtinLocation == null);
                    if (builtinLocation != null)
                        FormatLocationFromReferenceEntry(builtinLocation, AddressableReferencerDefaultObject.Settings.BuiltInBundleEntry);

                }
            }
        }
        
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

        private void FormatLocationFromReferenceEntry(ContentCatalogDataEntry catalogLocation, AddressableReferenceEntry referenceEntry)
        {
            catalogLocation.InternalId = referenceEntry.baseInternalId.Replace("{BuildTarget}", EditorUserBuildSettings.activeBuildTarget.ToString());

            // Path handling for windows targets, the slashes of the primary key musn't be replaced by backslashes
            if (IResourceLocationExtension.PathSeparatorForPlatform(EditorUserBuildSettings.activeBuildTarget) == '\\')
            {
                string pk = Regex.Replace(catalogLocation.Keys.First().ToString(), "_?[0-9a-f]{32}.bundle", "");
                string bpk = pk.Replace("/", "\\");

                catalogLocation.InternalId = catalogLocation.InternalId.Replace(bpk, pk);
            }
        }

        static string StripHashFromBundleLocation(string hashedBundleLocation)
        {
            return hashedBundleLocation.Remove(hashedBundleLocation.LastIndexOf('_')) + ".bundle";
        }

    }
}
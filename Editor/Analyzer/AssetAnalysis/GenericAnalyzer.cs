using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.U2D;
using static UnityEditor.AddressableAssets.Build.Layout.BuildLayout;
using static UnityEditor.FilePathAttribute;

namespace AddressableReferencer.Editor.Analyzer {

    public class GenericAnalyzer
    {

        public static GenericAnalyzer GetAnalyzer(string assetType, BundleAnalyzer parentAnalyzer)
        {

            return assetType switch
            {
                "Folder" => new FolderAnalyzer(parentAnalyzer),
                "SpriteAtlas" => new SpriteAtlasAnalyzer(parentAnalyzer),
                "GameObject" => new PrefabAnalyzer(parentAnalyzer),
                _ => new GenericAnalyzer(parentAnalyzer),
            };

        }

        protected BundleAnalyzer m_parentAnalyzer;

        protected BundledAssetGroupSchema BundleSchema { get { return m_parentAnalyzer.schema; } }
        protected AddressableReferenceSchema ReferenceSchema { get { return m_parentAnalyzer.referenceSchema; } }
        protected AddressableAssetGroup AssetGroup { get { return m_parentAnalyzer.assetGroup; } }
        protected IResourceLocation Location { get { return m_parentAnalyzer.location; } }
        protected AssetsManager AssetManager { get { return m_parentAnalyzer.mgr; } }
        protected AssetsFileInstance CabFile { get { return m_parentAnalyzer.CABFile; } }
        protected AssetsFileInstance MonoscriptFile { get { return m_parentAnalyzer.monoscriptFile; } }
        protected AssetFileInfo AssetBundle { get { return m_parentAnalyzer.assetBundle; } }
        protected int AssetCount { get { return m_parentAnalyzer.AssetCount; } }
        protected string[] Labels { get { return m_parentAnalyzer.Labels; } }
        
        public GenericAnalyzer(BundleAnalyzer parentAnalyzer)
        {
            m_parentAnalyzer = parentAnalyzer;
        }

        public virtual (AddressableAssetEntry, List<ObjectMapping>) Analyze(long pathId, string assetPath)
        {
            var assetExt = AssetManager.GetExtAsset(CabFile, 0, pathId);

            if (CheckMissingAsset(assetPath, pathId, out var assetGUID, out var newPath))
            {
                Debug.LogWarning($"Couldn't find or create asset {assetPath}");
                return (null, null);
            }

            AddressableAssetEntry entry = CreateOrGetAssetEntry(assetGUID, newPath);

            var createdReference = CreateAssetReference(pathId, assetGUID, newPath);


            List<ObjectMapping> references = new();

            if (createdReference != null)
                references.Add(createdReference);

            return (entry, references);
        }

        public virtual AddressableAssetEntry CreateOrGetAssetEntry(string assetGUID, string assetPath = null)
        {
            if (AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(assetGUID) != null)
            {
                var oldEntry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(assetGUID);
                if (oldEntry.parentGroup.SchemaTypes.Contains(typeof(AddressableReferenceSchema)))
                    return oldEntry;
            }

            var entry = AddressableAssetSettingsDefaultObject.Settings.CreateOrMoveEntry(
                assetGUID,
                AssetGroup,
                false,
                true
            );

            if (entry != null)
                entry.SetAddress(assetPath);

            if (Labels != null)
            {
                foreach(var label in Labels)
                    entry.SetLabel(label, true);
            }

            return entry;
        }

        public virtual ObjectMapping CreateAssetReference(long pathId, string assetGUID, string assetPath)
        {

            var assetExt = AssetManager.GetExtAsset(CabFile, 0, pathId);
            
            

            ObjectIdentifier obid = new();
            ObjectIdentifier[] assetRepresentations = ContentBuildInterface.GetPlayerAssetRepresentations(new GUID(assetGUID), EditorUserBuildSettings.activeBuildTarget);

            foreach (var oid in assetRepresentations)
            {
                var o = ObjectIdentifier.ToObject(oid);

                if (CheckAsset(o, assetExt))
                {
                    obid = oid;
                }

            }

            if (obid.localIdentifierInFile != 0)
                return new ObjectMapping(obid, pathId);

            Debug.LogWarning($"Asset:{assetPath} of type {assetExt.baseField.TypeName} couldn't be matched to an asset representation.");
            
            return null;

        }
        
        protected bool CheckAsset(UnityEngine.Object obj, AssetExternal assetExt)
        {

            string actualType = assetExt.baseField.TypeName;

            if (actualType == "MonoBehaviour")
            {
                actualType = AssetManager.GetExtAsset(MonoscriptFile, 0, assetExt.baseField["m_Script.m_PathID"].AsLong).baseField["m_ClassName"].AsString;
            }

            if (actualType == "Shader")
            {
                return true;
            }

            Debug.Log($"Type : id{assetExt.info.TypeId} {Enum.GetName(typeof(AssetClassID), assetExt.info.TypeId)}");

            return (obj.GetType().Name == actualType && obj.name == assetExt.baseField["m_Name"].AsString);

        }

        protected bool CheckMissingAsset(string assetPath, long pathId, out string assetGUID, out string newPath)
        {
            newPath = assetPath;
            assetGUID = AssetDatabase.AssetPathToGUID(assetPath, AssetPathToGUIDOptions.OnlyExistingAssets);

            // var assetDep = new AssetDependencies(mgr, CABFile);
            if (assetGUID.Equals(""))
            {

                var tempAssetGUID = SearchAssetMultipleFormat(assetPath, pathId);

                if (tempAssetGUID.Equals(""))
                {
                    assetGUID = CreateMissingAsset(AssetManager.GetExtAsset(CabFile, 0, pathId).baseField.TypeName, assetPath, pathId);
                }
                else
                {
                    assetGUID = tempAssetGUID;
                    newPath = AssetDatabase.GUIDToAssetPath(assetGUID);
                }

            }

            return assetGUID.Equals("");

        }

        protected string SearchAssetMultipleFormat(string assetPath, long pathId)
        {

            var assetGUID = string.Empty;

            var assetExt = AssetManager.GetExtAsset(CabFile, 0, pathId);

            var internalAssetName = assetExt.baseField["m_Name"].AsString;
            var extension = assetPath.Split(".").Last();
            var basePath = assetPath.Replace($".{extension}", "");
            var folderPath = Path.GetDirectoryName(assetPath);

            // GG to the person who managed to put an extra space in the container path or asset name
            assetGUID = AssetDatabase.AssetPathToGUID($"{basePath.Trim()}.{extension}", AssetPathToGUIDOptions.OnlyExistingAssets);
            if (!assetGUID.Equals(""))
                return assetGUID;

            // Sometimes there are comma in the container path?
            assetGUID = AssetDatabase.AssetPathToGUID($"{assetPath.Replace(",", "_")}", AssetPathToGUIDOptions.OnlyExistingAssets);
            if (!assetGUID.Equals(""))
                return assetGUID;

            // In case someone makes a clone and forget it is one
            assetGUID = AssetDatabase.AssetPathToGUID($"{assetPath.Replace("(Clone)", "")}", AssetPathToGUIDOptions.OnlyExistingAssets);
            if (!assetGUID.Equals(""))
                return assetGUID;


            // Alternate path using the Asset name in the bundle instead of the container path
            assetGUID = AssetDatabase.AssetPathToGUID($"{folderPath}/{internalAssetName}.{extension}", AssetPathToGUIDOptions.OnlyExistingAssets);
            if (!assetGUID.Equals(""))
                return assetGUID;


            var formats = FileFormatList.GetFormatList(assetExt.baseField.TypeName);
            if (formats != null && assetGUID.Equals(""))
            {
                foreach (var format in formats)
                {

                    assetGUID = AssetDatabase.AssetPathToGUID($"{basePath}{format}", AssetPathToGUIDOptions.OnlyExistingAssets);

                    if (!assetGUID.Equals(""))
                        return assetGUID;
                }
            }

            return assetGUID;
        }
        
        /// <summary>
        /// TODO Make it a post processing task because there might be multiple assets with the same container paths 
        /// but the asset doesn't exist yet and we don't want to create the wrong one
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="pathId"></param>
        /// <returns>Asset GUID</returns>
        protected string CreateMissingAsset(string assetType, string assetPath, long pathId)
        {
            return GenericBuilder.GetAnalyzer(assetType, m_parentAnalyzer).CreateMissingAsset(pathId, assetPath);
        }

    }


    /// <summary>
    /// Generic assetAnalyzer class
    /// </summary>
    /// <typeparam name="TObject"></typeparam>
    public class GenericAnalyzerT<TObject> : GenericAnalyzer where TObject : UnityEngine.Object
    {
        public GenericAnalyzerT(BundleAnalyzer parentAnalyzer) : base(parentAnalyzer)
        {
        }
    }  

}
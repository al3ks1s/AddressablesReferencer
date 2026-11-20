using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;


namespace AddressableReferencer.Editor.Analyzer.AssetBuilder
{
    public class GenericBuilder
    {

        public static GenericBuilder GetBuilder(string assetType, BundleAnalyzer parentAnalyzer)
        {
            return assetType switch
            {
                "SpriteAtlas" => new SpriteAtlasBuilder(parentAnalyzer),
                _ => new GenericBuilder(parentAnalyzer),
            };
        }

        BundleAnalyzer m_parentAnalyzer;

        protected BundledAssetGroupSchema BundleSchema { get { return m_parentAnalyzer.schema; } }
        protected AddressableReferenceSchema ReferenceSchema { get { return m_parentAnalyzer.referenceSchema; } }
        protected AddressableAssetGroup AssetGroup { get { return m_parentAnalyzer.assetGroup; } }
        protected IResourceLocation Location { get { return m_parentAnalyzer.location; } }
        protected AssetsManager AssetManager { get { return m_parentAnalyzer.mgr; } }
        protected AssetsFileInstance CabFile { get { return m_parentAnalyzer.CABFile; } }
        protected AssetsFileInstance MonoscriptFile { get { return m_parentAnalyzer.monoscriptFile; } }
        protected AssetFileInfo AssetBundle { get { return m_parentAnalyzer.assetBundle; } }
        protected int AssetCount { get { return m_parentAnalyzer.AssetCount; } }

        public GenericBuilder(BundleAnalyzer parentAnalyzer)
        {
            m_parentAnalyzer = parentAnalyzer;
        }

        public virtual string CreateMissingAsset(long pathId, string assetPath)
        {
            var assetExt = AssetManager.GetExtAsset(CabFile, 0, pathId, true);
            Debug.LogWarning($"Create missing asset unimplemented for {assetPath} of type {GetAssetTypeName(assetExt)}");
            return string.Empty;
        }

        protected bool CheckAndCreateMissingFolder(string assetPath)
        {
            string folderPath = Path.GetDirectoryName(assetPath);

            if (AssetDatabase.IsValidFolder(folderPath))
                return true;

            // By design, the root should always be Assets/ so it shouldn't be an infinite loop
            if (CheckAndCreateMissingFolder(folderPath))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folderPath), Path.GetFileNameWithoutExtension(folderPath));
                return true;
            }

            return false;
        }

        protected string GetAssetTypeName(AssetExternal assetExt)
        {
            string assetType = Enum.GetName(typeof(AssetClassID), assetExt.info.TypeId) ?? assetExt.info.TypeId.ToString();

            if (assetExt.info.TypeId == (int)AssetClassID.MonoBehaviour)
            {
                ushort idx = assetExt.info.GetScriptIndex(CabFile.file);
                if (idx != 0xFFFF && idx < CabFile.file.Metadata.ScriptTypes.Count)
                {
                    long monoscriptPathID = CabFile.file.Metadata.ScriptTypes[idx].PathId;
                    assetType = AssetManager.GetExtAsset(MonoscriptFile, 0, monoscriptPathID).baseField["m_ClassName"].AsString;
                }
            }

            return assetType;
        }

    }

    public class GenericBuilderT<TObject> : GenericBuilder where TObject : UnityEngine.Object
    {
        public GenericBuilderT(BundleAnalyzer parentAnalyzer) : base(parentAnalyzer)
        {
        }
    }
}
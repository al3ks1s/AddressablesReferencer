using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace AddressableReferencer.Editor.Settings
{
    [Serializable]
    public class AddressableReferencerSettings : ScriptableObject
    {

        public static AddressableReferencerSettings Create(string folder, string assetName)
        {

            AddressableReferencerSettings ars = CreateInstance<AddressableReferencerSettings>();
            string assetPath = folder + "/" + assetName + ".asset";

            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath, AssetPathToGUIDOptions.OnlyExistingAssets)))
                return AssetDatabase.LoadAssetAtPath<AddressableReferencerSettings>(assetPath);

            AssetDatabase.CreateAsset(ars, assetPath);
            ars = AssetDatabase.LoadAssetAtPath<AddressableReferencerSettings>(assetPath);

            AssetDatabase.SaveAssets();

            return ars;
        }


        [SerializeField]
        private string m_ExternalStreamingAssetsFolder;
        public string ExternalStreamingAssetsFolder
        {
            get { return m_ExternalStreamingAssetsFolder; }
            set { m_ExternalStreamingAssetsFolder = value; Save(); }
        }


        [SerializeField]
        private bool m_MoveCatalogToSharedBundleBuildPath;
        public bool MoveCatalogToSharedBundleBuildPath
        {
            get { return m_MoveCatalogToSharedBundleBuildPath; }
            set { m_MoveCatalogToSharedBundleBuildPath = value; Save(); }
        }


        [SerializeField]
        private AddressableReferenceEntry m_builtInBundleReferenceEntry;
        public AddressableReferenceEntry BuiltInBundleEntry
        {
            get { return m_builtInBundleReferenceEntry; }
            set { m_builtInBundleReferenceEntry = value; Save(); }
        }


        [SerializeField]
        private bool m_useBaseGameBuiltinAssets;
        public bool UseBaseGameBuiltinAssets
        {
            get { return m_useBaseGameBuiltinAssets; }
            set { m_useBaseGameBuiltinAssets = value; Save(); }
        }


        public void Save()
        {
            EditorUtility.SetDirty(this);
        }
    }
}
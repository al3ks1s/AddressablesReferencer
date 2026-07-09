using System;
using UnityEditor;
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
            set { m_ExternalStreamingAssetsFolder = value; EditorUtility.SetDirty(this); }
        }

        [SerializeField]
        private bool m_MoveCatalogToSharedBundleBuildPath;

        public bool MoveCatalogToSharedBundleBuildPath
        {
            get { return m_MoveCatalogToSharedBundleBuildPath; }
            set { m_MoveCatalogToSharedBundleBuildPath = value; EditorUtility.SetDirty(this); }
        }
    }
}
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
        private List<BuildTarget> m_buildTargetsForCatalog;
        public List<BuildTarget> BuildTargetsForCatalog
        {
            get { 
                if (m_buildTargetsForCatalog == null) 
                    m_buildTargetsForCatalog = new List<BuildTarget>();

                return m_buildTargetsForCatalog;
            }
        }
        public bool IsBuildTargetActive(BuildTarget target)
        {
            return BuildTargetsForCatalog.Contains(target);
        }
        public void AddBuildTargetForCatalog(BuildTarget target)
        {
            if (!IsBuildTargetActive(target))
                BuildTargetsForCatalog.Add(target);

            Save();
        }
        public void RemoveBuildTargetForCatalog(BuildTarget target)
        {
            if (IsBuildTargetActive(target))
                BuildTargetsForCatalog.Remove(target);

            Save();
        }
        public void ClearBuildTargetForCatalogList()
        {
            BuildTargetsForCatalog.Clear();
            Save();
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
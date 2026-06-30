using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace AddressableReferencer.Editor.Analyzer { 

    public class AnalyzerWindow : EditorWindow
    {

        string StreamingAssetFolder = "Enter the Streaming assets folder here";
        string setupDangerZoneString = "Danger Zone - Setup utility, this will completely reset the generated addressable data and referencer setup";
        bool activeDangerZone = false;

        [MenuItem("Window/Asset Management/Addressable Referencer/Analyzer")]
        public static void Open()
        {
            GetWindow<AnalyzerWindow>("Addressables Referencer - Analyzer");
        }

        public void OnEnable()
        {
            if (AddressableReferencerDefaultObject.SettingsExists)
            {
                if (!string.IsNullOrEmpty(AddressableReferencerDefaultObject.Settings.ExternalStreamingAssetsFolder))
                    StreamingAssetFolder = AddressableReferencerDefaultObject.Settings.ExternalStreamingAssetsFolder;
            }
        }

        public void OnGUI()
        {

            StreamingAssetFolder = EditorGUILayout.TextField(StreamingAssetFolder);

            if (GUILayout.Button("Run stuff"))
            {
                lesgo();
            }

            if (GUILayout.Button("Test stuff"))
            {
                fastTest();
            }

            EditorGUILayout.Space(25);

            if (GUILayout.Button("Clear reference Groups"))
            {
                ClearAddressableGroups();
            }

            EditorGUILayout.Space(25);

            activeDangerZone = EditorGUILayout.BeginFoldoutHeaderGroup(activeDangerZone, setupDangerZoneString);

            if (activeDangerZone) { 
                if (GUILayout.Button("Setup stuff"))
                {
                    SetupPackage();
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

        }

        public void lesgo()
        {

            var so = AddressableReferencerDefaultObject.Settings;

            AddressableReferencerDefaultObject.Settings.ExternalStreamingAssetsFolder = StreamingAssetFolder;
        
            CatalogAnalyzer cat = new(AddressableReferencerDefaultObject.Settings.ExternalStreamingAssetsFolder);

            cat.LoadCatalog(Path.Join(cat.StreamingAssetsPath, "catalog.bin"));
            cat.ProcessGroups();

        }

        public void fastTest()
        {

        }

        public void SetupPackage()
        {
            var settings = AddressableReferencerDefaultObject.Settings;

            // Replace the build script
            string BuildScriptPath = AddressableAssetSettingsDefaultObject.Settings.DataBuilderFolder + "/" + typeof(BuildScriptReferenceMode).Name + ".asset";

            string guid = AssetDatabase.AssetPathToGUID(BuildScriptPath, AssetPathToGUIDOptions.OnlyExistingAssets);

            if (!string.IsNullOrEmpty(guid))
            {
                var a = AssetDatabase.LoadAssetAtPath<BuildScriptReferenceMode>(BuildScriptPath);
                var builderIndex = AddressableAssetSettingsDefaultObject.Settings.DataBuilders.IndexOf(a);

                AddressableAssetSettingsDefaultObject.Settings.DataBuilders.RemoveAt(builderIndex);

                AssetDatabase.DeleteAsset(BuildScriptPath);
                AssetDatabase.SaveAssets();
            }

            BuildScriptReferenceMode.GetAsset();

            // Clear out Addressable groups
            ClearAddressableGroups();

            activeDangerZone = false;

        }

        public void ClearAddressableGroups()
        {

            var groups = AddressableAssetSettingsDefaultObject.Settings.groups.Where(g => g.SchemaTypes.Contains(typeof(AddressableReferenceSchema)));
            foreach (var group in groups.ToArray())
            {
                AddressableAssetSettingsDefaultObject.Settings.RemoveGroup(group);
            }

        }


    }
}
using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;


namespace AddressableReferencer.Editor.Build
{

    using Debug = UnityEngine.Debug;

    [CreateAssetMenu(fileName = "BuildScriptReferenceMode.asset", menuName = "Addressables Referencer/BuildScriptReferenceMode")]
    public class BuildScriptReferenceMode : BuildScriptPackedMode
    {
        public override string Name
        {
            get { return "Reference Build Script"; }
        }

        public override BuildScriptSchemaDriven CreateSchemaDrivenBuildScript()
        {
            //throw new NotImplementedException("Building with references isn't supported on this branch of Addressables Referencer, please use either Addressables-3 or Addressables-4");
            return CreateInstance<BuildScriptReferenceSchemaDriven>();
        }

        public static BuildScriptReferenceMode GetAsset()
        {
            var asset = CreateScriptAsset<BuildScriptReferenceMode>();
            AddressableAssetSettingsDefaultObject.Settings.DataBuilders.Add(asset);
            return asset;
        }

        internal static T CreateScriptAsset<T>() where T : ScriptableObject
        {
            var DataBuilderFolder = AddressableAssetSettingsDefaultObject.Settings.DataBuilderFolder;

            var script = CreateInstance<T>();
            if (!Directory.Exists(DataBuilderFolder))
                Directory.CreateDirectory(DataBuilderFolder);
            var path = DataBuilderFolder + "/" + typeof(T).Name + ".asset";
            if (!File.Exists(path))
            {
                AssetDatabase.CreateAsset(script, path);
                return script;
            }
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}

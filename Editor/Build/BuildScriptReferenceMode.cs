using UnityEngine;
using UnityEngine.Android;

namespace UnityEditor.AddressableAssets.Build.DataBuilders
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
            return CreateInstance<BuildScriptReferenceSchemaDriven>();
        }

        public static BuildScriptReferenceMode GetAsset()
        {
            var asset = AddressableAssetSettingsDefaultObject.Settings.CreateScriptAsset<BuildScriptReferenceMode>();
            AddressableAssetSettingsDefaultObject.Settings.DataBuilders.Add(asset);
            return asset;
        }


    }
}

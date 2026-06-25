using UnityEditor;
using UnityEngine;

public class AddressableReferencerDefaultObject : ScriptableObject
{

    public const string kName = "AddressableReferencer";

    public const string kDefaultSettingAssetName = kName + "Setting";
    public const string kDefaultSettingFolder = "Assets/"+ kName + "Data";
    public const string kDefaultSettingObjectNale = "fr.al3ks1s.addressablereferencer";

    public string DefaultAssetPath 
    { 
        get { return kDefaultSettingFolder + "/" + kDefaultSettingAssetName + ".asset"; }  
    }

    public void EnsureFolderExists()
    {
        if (AssetDatabase.IsValidFolder(kDefaultSettingFolder))
            return;
        AssetDatabase.CreateFolder("Assets", kName + "Data");
    }

    [SerializeField]
    internal string m_AddressableReferencerSettingsGuid;

    internal AddressableReferencerSettings s_DefaultSetting;

    public AddressableReferencerSettings Settings
    {
        get { return s_DefaultSetting; }
    }


}

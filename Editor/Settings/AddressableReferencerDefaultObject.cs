using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public class AddressableReferencerDefaultObject : ScriptableObject
{

    public const string kName = "AddressableReferencer";

    public const string kDefaultSettingAssetName = kName + "Setting";
    public const string kDefaultSettingFolder = "Assets/"+ kName + "Data";
    public const string kDefaultSettingObjectName = "fr.al3ks1s.addressablereferencer";

    public string DefaultAssetPath 
    { 
        get { return kDefaultSettingFolder + "/" + kDefaultSettingAssetName + ".asset"; }  
    }

    public static void EnsureFolderExists()
    {
        if (AssetDatabase.IsValidFolder(kDefaultSettingFolder))
            return;
        AssetDatabase.CreateFolder("Assets", kName + "Data");
    }

    [SerializeField]
    internal string m_AddressableReferencerSettingsGuid;

    internal static AddressableReferencerSettings s_DefaultSettingsObject;

    bool m_LoadingSettingsObject;

    internal AddressableReferencerSettings LoadSettingsObject()
    {

        if (m_LoadingSettingsObject)
        {
            Debug.LogWarning("Detected stack overflow when accessing AddressableAssetSettingsDefaultObject.Settings object.");
            return null;
        }

        if (string.IsNullOrEmpty(m_AddressableReferencerSettingsGuid))
        {
            Debug.LogError("Invalid guid for default AddressableReferencerSettings object.");
            return null;
        }
        
        var path = AssetDatabase.GUIDToAssetPath(m_AddressableReferencerSettingsGuid); 
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogErrorFormat("Unable to determine path for default AddressableAssetSettings object with guid {0}.", m_AddressableReferencerSettingsGuid);
            return null;
        }
        
        m_LoadingSettingsObject = true;
        var settings = AssetDatabase.LoadAssetAtPath<AddressableReferencerSettings>(path);
        m_LoadingSettingsObject = false;

        return settings;
    }

    internal void SetSettingObject(AddressableReferencerSettings ars)
    {
        if (ars == null)
        {
            m_AddressableReferencerSettingsGuid = null;
            return;
        }

        var path = AssetDatabase.GetAssetPath(ars);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError($"Unable to determine path for Addressable Referencer Settings with guid {m_AddressableReferencerSettingsGuid}");
            return;
        }

        m_AddressableReferencerSettingsGuid = AssetDatabase.AssetPathToGUID(path);

    }

    public static bool SettingsExists
    {
        get
        {
            AddressableReferencerDefaultObject so;
            if (EditorBuildSettings.TryGetConfigObject<AddressableReferencerDefaultObject>(kDefaultSettingObjectName, out so))
                return !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(so.m_AddressableReferencerSettingsGuid));
            return false;
        }
    }


    public static AddressableReferencerSettings Settings
    {
        get 
        {

            if (s_DefaultSettingsObject == null)
            {

                AddressableReferencerDefaultObject so;
                if (EditorBuildSettings.TryGetConfigObject<AddressableReferencerDefaultObject>(kDefaultSettingObjectName, out so))
                {
                    EnsureFolderExists();
                    s_DefaultSettingsObject = so.LoadSettingsObject();
                }
                else
                {
                    EnsureFolderExists();
                    Settings = AddressableReferencerSettings.Create(kDefaultSettingFolder, kDefaultSettingAssetName);
                }

            }

            return s_DefaultSettingsObject;
        }
        set
        {

            if (value != null)
            {
                var path = AssetDatabase.GetAssetPath(value);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogErrorFormat("AddressableReferencerSettings object must be saved to an asset before it can be set as the default.");
                    return;
                }
            }

            s_DefaultSettingsObject = value;
            AddressableReferencerDefaultObject so;

            if (!EditorBuildSettings.TryGetConfigObject<AddressableReferencerDefaultObject>(kDefaultSettingObjectName, out so))
            {
                so = CreateInstance<AddressableReferencerDefaultObject>();

                EnsureFolderExists();
                AssetDatabase.CreateAsset(so, kDefaultSettingFolder + "/DefaultObject.asset");
                AssetDatabase.SaveAssets();
                EditorBuildSettings.AddConfigObject(kDefaultSettingObjectName, so, true);

            }

            so.SetSettingObject(s_DefaultSettingsObject);
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();

        }
    }
    
    public static void InitialSetup()
    {
        var settings = AddressableReferencerDefaultObject.Settings;

        // Replace the build script
        string BuildScriptPath = AddressableAssetSettingsDefaultObject.Settings.DataBuilderFolder + "/" + typeof(BuildScriptReferenceMode).Name + ".asset";
        string guid = AssetDatabase.AssetPathToGUID(BuildScriptPath, AssetPathToGUIDOptions.OnlyExistingAssets);

        if (!string.IsNullOrEmpty(guid))
        {
            var a = AssetDatabase.LoadAssetAtPath<BuildScriptReferenceMode>(BuildScriptPath);
            var builderIndex = AddressableAssetSettingsDefaultObject.Settings.DataBuilders.IndexOf(a);

            if (builderIndex < AddressableAssetSettingsDefaultObject.Settings.DataBuilders.Count && builderIndex >= 0)
                AddressableAssetSettingsDefaultObject.Settings.DataBuilders.RemoveAt(builderIndex);

            AssetDatabase.DeleteAsset(BuildScriptPath);
            AssetDatabase.SaveAssets();
        }

        BuildScriptReferenceMode.GetAsset();

        // Add the variables
        if (!AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetVariableNames().Contains("Addressable References.BuildPath"))
            AddressableAssetSettingsDefaultObject.Settings.profileSettings.CreateValue("Addressable References.BuildPath", "[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]-Reference");

        if (!AddressableAssetSettingsDefaultObject.Settings.profileSettings.GetVariableNames().Contains("Addressable References.LoadPath"))
            AddressableAssetSettingsDefaultObject.Settings.profileSettings.CreateValue("Addressable References.LoadPath", "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]");
    }

    public static void ClearSettings()
    {
        AddressableAssetSettingsDefaultObject.Settings.profileSettings.RemoveValue("Addressable References.BuildPath");
        AddressableAssetSettingsDefaultObject.Settings.profileSettings.RemoveValue("Addressable References.LoadPath");

        EditorBuildSettings.TryGetConfigObject<AddressableReferencerDefaultObject>(kDefaultSettingObjectName, out var so);

        if (so == null)
            return;

        if (!AssetDatabase.GUIDToAssetPath(so.m_AddressableReferencerSettingsGuid).Equals(string.Empty))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(so.m_AddressableReferencerSettingsGuid));

        EditorBuildSettings.RemoveConfigObject(kDefaultSettingObjectName);
        AssetDatabase.DeleteAsset(kDefaultSettingFolder + "/DefaultObject.asset");
    }

}

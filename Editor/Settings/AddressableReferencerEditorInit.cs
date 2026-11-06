#define ADDRESSABLES_REFERENCER_AVAILABLE

using UnityEditor;
using UnityEngine;

namespace AddressableReferencer.Editor.Settings { 

    [InitializeOnLoad]
    public class AddressableReferencerEditorInit : ScriptableObject
    {  
        
        private const string m_EditorInitializedBoolName = nameof(m_EditorInitializedBoolName);

        static AddressableReferencerEditorInit()
        {
            if (!AddressableReferencerDefaultObject.SettingsExists) { 
                AddressableReferencerDefaultObject.InitialSetup();            
            }
        }
    }
}
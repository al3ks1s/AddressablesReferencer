using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;

namespace AddressableReferencer.Editor.Settings { 

    [InitializeOnLoad]
    public class AddressableReferencerEditorInit : ScriptableObject
    {
        
        private const string m_EditorInitializedBoolName = nameof(m_EditorInitializedBoolName);

        static AddressableReferencerEditorInit()
        {
            
        }
    
    }
}
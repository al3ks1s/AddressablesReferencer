using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

namespace UnityEditor.AddressableAssets.Settings.GroupSchemas
{
    [DisplayName("Addressable Reference")]
    public class AddressableReferenceSchema : AddressableAssetGroupSchema
    {

        [SerializeField]
        bool m_ReferenceEnabled = true;

        public bool ReferenceEnabled
        {
            get { return m_ReferenceEnabled; }
            set
            {
                if (m_ReferenceEnabled != value)
                {
                    m_ReferenceEnabled = value;
                    SetDirty(true);
                }
            }
        }


        public List<AddressableReferenceEntry> m_Entries;

        public List<AddressableReferenceEntry> Entries
        {
            get 
            {
                if (m_Entries == null)
                    m_Entries = new();

                return m_Entries; 
            }
            set
            {
                if (m_Entries != value)
                {
                    m_Entries = value;
                    SetDirty(true);
                }
            }
        }

        public int EntryCount
        {
            get { return Entries.Count; }
        }

        private List<int> ObjectCounts
        {
            get 
            { 
                return Entries.Select(e => e.ObjectMappingDict.Count).ToList();
            }
        }

        public int ObjectCount
        {
            get
            {
                return ObjectCounts.Sum();
            }
        }

        private GUIContent m_ReferenceEnabledGuiContent = new GUIContent("Enable Reference", "This addressable group will be used to generate reference at build time.");

        /// <inheritdoc/>
        public override void OnGUI()
        {
            var staticContent = EditorGUILayout.Toggle(m_ReferenceEnabledGuiContent, m_ReferenceEnabled);
            if (staticContent != m_ReferenceEnabled)
            {
                var prop = SchemaSerializedObject.FindProperty("m_ReferenceEnabled");
                prop.boolValue = staticContent;
                SchemaSerializedObject.ApplyModifiedProperties();
            }
        }

        /// <inheritdoc/>
        public override void OnGUIMultiple(List<AddressableAssetGroupSchema> otherSchemas)
        {
            string propertyName = "m_ReferenceEnabled";
            var prop = SchemaSerializedObject.FindProperty(propertyName);

            // Type/Static Content
            ShowMixedValue(prop, otherSchemas, typeof(bool), propertyName);
            EditorGUI.BeginChangeCheck();

            var staticContent = EditorGUILayout.Toggle(m_ReferenceEnabledGuiContent, m_ReferenceEnabled);

            if (EditorGUI.EndChangeCheck())
            {
                prop.boolValue = staticContent;
                SchemaSerializedObject.ApplyModifiedProperties();
                foreach (var s in otherSchemas)
                {
                    var otherProp = s.SchemaSerializedObject.FindProperty(propertyName);
                    otherProp.boolValue = staticContent;
                    s.SchemaSerializedObject.ApplyModifiedProperties();
                }
            }

            EditorGUI.showMixedValue = false;
        }

        public void SaveData()
        {
            SetDirty(true);
        }


    }



}
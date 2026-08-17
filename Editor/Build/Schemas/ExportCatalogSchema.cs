using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEditor.AddressableAssets.GUI;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
namespace UnityEditor.AddressableAssets.Settings.GroupSchemas
{
    /// <summary>
    /// 
    /// </summary>
    [DisplayName("Export Catalog to Group Build Location")]
    public class ExportCatalogSchema : AddressableAssetGroupSchema
    {

        public class BuildTargetPopup : PopupWindowContent
        {

            ExportCatalogSchema m_schema;

            public BuildTargetPopup(ExportCatalogSchema schema)
            {
                m_schema = schema;
            }

            public override Vector2 GetWindowSize() => new Vector2(200, m_schema.AvailableTargets.Count * 20 + 10);

            public override void OnGUI(Rect rect)
            {
                foreach (var target in m_schema.AvailableTargets)
                {
                    bool isSelected = m_schema.IsBuildTargetActive(target);
                    bool newValue = EditorGUILayout.ToggleLeft(target.ToString(), isSelected);
                    if (newValue != isSelected)
                    {
                        if (newValue) m_schema.AddBuildTargetForCatalog(target);
                        else m_schema.RemoveBuildTargetForCatalog(target);
                    }
                }
            }

        }


        [SerializeField]
        private bool m_enableExport = true;
        public bool EnableExport
        {
            get { return m_enableExport; }
            set { m_enableExport = value; SetDirty(true); }
        }

        [SerializeField]
        private bool m_exportForBuildTargets;
        public bool ExportForBuildTargets
        {
            get { return m_exportForBuildTargets; }
            set { m_exportForBuildTargets = value; SaveData(); }
        }

        private List<BuildTarget> AvailableTargets
        {
            get 
            {
                List<BuildTarget> targets = new List<BuildTarget>();

                foreach (var targetValue in Enum.GetValues(typeof(BuildTarget)))
                {
                    if (BuildPipeline.IsBuildTargetSupported(EditorUserBuildSettings.selectedBuildTargetGroup, (BuildTarget)targetValue))
                    {
                        targets.Add((BuildTarget)targetValue);
                    }
                }

                return targets;
            }
        }

        [SerializeField]
        private List<BuildTarget> m_buildTargetsForCatalog;
        public List<BuildTarget> BuildTargetsForCatalog
        {
            get
            {
                if (m_buildTargetsForCatalog == null)
                    m_buildTargetsForCatalog = new List<BuildTarget>();

                if (!m_buildTargetsForCatalog.Contains(EditorUserBuildSettings.activeBuildTarget))
                {
                    m_buildTargetsForCatalog.Add(EditorUserBuildSettings.activeBuildTarget);
                }

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

            SaveData();
        }
        public void RemoveBuildTargetForCatalog(BuildTarget target)
        {
            if (IsBuildTargetActive(target))
                BuildTargetsForCatalog.Remove(target);

            SaveData();
        }
        public void ClearBuildTargetForCatalogList()
        {
            BuildTargetsForCatalog.Clear();
            SaveData();
        }
        public void AddAllTargetsToCatalogList()
        {
            foreach (var targetValue in Enum.GetValues(typeof(BuildTarget)))
            {
                if (BuildPipeline.IsBuildTargetSupported(EditorUserBuildSettings.selectedBuildTargetGroup, (BuildTarget)targetValue))
                {
                    AddBuildTargetForCatalog((BuildTarget)targetValue);
                }
            }
        }


        public override string CanEnableSchema()
        {

            List<AddressableAssetGroup> otherGroupsWithExportCatalogSchema = 
                AddressableAssetSettingsDefaultObject.Settings.groups.Where(g => 
                    g.HasSchema<ExportCatalogSchema>() && 
                    !(g == this.Group) && 
                    g.HasSchema<BundledAssetGroupSchema>() && 
                    g.GetSchema<BundledAssetGroupSchema>().BuildPath == Group.GetSchema<BundledAssetGroupSchema>().BuildPath
                ).ToList();


            foreach (var otherGroup in otherGroupsWithExportCatalogSchema)
            {
                Debug.Log(otherGroup.Name);

                var ogSchema = otherGroup.GetSchema<ExportCatalogSchema>();

                if (ogSchema.EnableExport != EnableExport ||
                    ogSchema.ExportForBuildTargets != ExportForBuildTargets)
                    return $"Two or more Export Catalog Schemas pointing toward the same build path have different options enabled. The build script will merge them into the broadest possible options.";
            }

            return "";
        }


        private GUIContent m_ExportEnabledGuiContent = new GUIContent("Generate Catalog", "Export additional catalogs for this group. The catalogs will be moved to the group's build path. Different Addressable groups exporting to the same Build Path will be merged into a single one.");
        private GUIContent m_ExportCatalogForBuildTarget = new GUIContent("Multiple Build Targets", "Option to allow the build pipeline to generate multiple catalogs at once for this group for different build targets.");
        private GUIContent m_ExportCatalogForBuildTargets = new GUIContent("Build Targets", "List of the BuildTargets for which a catalog will be generated. The currently active BuildTarget will always get a catalog.");

        /// <inheritdoc/>
        public override void OnGUI()
        {
            var exportEnabledBool = EditorGUILayout.Toggle(m_ExportEnabledGuiContent, m_enableExport);
            if (exportEnabledBool != m_enableExport)
            {
                var prop = SchemaSerializedObject.FindProperty("m_enableExport");
                prop.boolValue = exportEnabledBool;
                SchemaSerializedObject.ApplyModifiedProperties();
            }

            var exportTargetBool = EditorGUILayout.Toggle(m_ExportCatalogForBuildTarget, m_exportForBuildTargets);
            if (exportTargetBool != m_exportForBuildTargets)
            {
                var prop = SchemaSerializedObject.FindProperty("m_exportForBuildTargets");
                prop.boolValue = exportTargetBool;
                SchemaSerializedObject.ApplyModifiedProperties();
            }

            if (ExportForBuildTargets)
            {

                string buttonText = string.Join(", ", BuildTargetsForCatalog);

                Rect fieldRect = EditorGUILayout.GetControlRect();
                Rect dropdownRect = EditorGUI.PrefixLabel(fieldRect, m_ExportCatalogForBuildTargets);

                if (EditorGUI.DropdownButton(dropdownRect, new GUIContent (buttonText), FocusType.Passive))
                {
                    try
                    {
                        PopupWindow.Show(dropdownRect, new BuildTargetPopup(this));
                    }
                    catch (Exception e)
                    {
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override void OnGUIMultiple(List<AddressableAssetGroupSchema> otherSchemas)
        {
            string propertyName = "m_enableExport";
            var prop = SchemaSerializedObject.FindProperty(propertyName);

            // Type/Static Content
            ShowMixedValue(prop, otherSchemas, typeof(bool), propertyName);
            EditorGUI.BeginChangeCheck();

            var staticContent = EditorGUILayout.Toggle(m_ExportEnabledGuiContent, m_enableExport);

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

        /// <summary>
        /// Marks the current object as dirty to save its data in editor.
        /// </summary>
        public void SaveData()
        {
            SetDirty(true);
            AssetDatabase.SaveAssetIfDirty(this);
        }
    }
}
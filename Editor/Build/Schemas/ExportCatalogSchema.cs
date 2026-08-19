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

        public static List<string> ExportCatalogExclusions = new() { "Local.BuildPath", "Remote.BuildPath", "Addressable References.BuildPath", string.Empty };
        public static bool IsBuildVarExcluded(string name)
        {
            return ExportCatalogExclusions.Contains(name);
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

        private GUIContent m_ExportEnabledGuiContent = new GUIContent("Generate Catalog", "Export additional catalogs for this group. The catalogs will be moved to the group's build path. Different Addressable groups exporting to the same Build Path will be merged into a single one.");
        private GUIContent m_ExportCatalogForBuildTarget = new GUIContent("Multiple Build Targets", "Option to allow the build pipeline to generate multiple catalogs at once for this group for different build targets.");
        private GUIContent m_ExportCatalogForBuildTargets = new GUIContent("Build Targets", "List of the BuildTargets for which a catalog will be generated. The currently active BuildTarget will always get a catalog.");

        /// <inheritdoc/>
        public override void OnGUI()
        {

            if (Group.HasSchema<BundledAssetGroupSchema>() && IsBuildVarExcluded(Group.GetSchema<BundledAssetGroupSchema>().BuildPath.GetName(Group.Settings)))
            {
                AddressablesGUIUtility.DrawErrorBoxWithLink(
                    $"This group is using one of the forbidden Build Path variables (or <custom>) : [{Group.GetSchema<BundledAssetGroupSchema>().BuildPath.GetName(Group.Settings)}], consider using a specific Build Path Variable for it.",
                    "Read more...",
                    "https://github.com/al3ks1s/AddressablesReferencer/blob/main/Documentation~/Addressables%20Referencer%20Usage.md#export-catalog-to-build-location-schema");
                GUILayout.Space(6);
            }

            if (!AreSchemaSynchronized())
            {

                var unSyncGroups = AddressableAssetSettingsDefaultObject.Settings.groups
                    .Where(g => g.HasSchema<ExportCatalogSchema>() && g.HasSchema<BundledAssetGroupSchema>())
                    .Where(g => g.GetSchema<BundledAssetGroupSchema>().BuildPath.Id == Group.GetSchema<BundledAssetGroupSchema>().BuildPath.Id)
                    .ToList();


                AddressablesGUIUtility.DrawErrorBoxWithLink(
                    $"The groups [{string.Join(", ", unSyncGroups.Except(new List<AddressableAssetGroup>() { unSyncGroups.Last() }).Select(g => g.Name))} and {unSyncGroups.Last().Name}] have the same Build Path Variable but different export options. The build pipeline will merge them into the broadest possible options. Please synchronize your Export Schemas to avoid unexpected errors.",
                    "Read more...",
                    "https://github.com/al3ks1s/AddressablesReferencer/blob/main/Documentation~/Addressables%20Referencer%20Usage.md#export-catalog-to-build-location-schema");
                GUILayout.Space(6);
            }

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
    

        
        public bool AreSchemaSynchronized()
        {
            var exportGroups = AddressableAssetSettingsDefaultObject.Settings.groups
                .Where(g => g != null)
                .Where(g => g.HasSchema<ExportCatalogSchema>() && g.HasSchema<BundledAssetGroupSchema>())
                .Where(g => g.GetSchema<BundledAssetGroupSchema>().BuildPath.Id == Group.GetSchema<BundledAssetGroupSchema>().BuildPath.Id);

            if (exportGroups.Any(g => g.GetSchema<ExportCatalogSchema>().IsEnabled != IsEnabled))
                return false;
            if (exportGroups.Any(g => g.GetSchema<ExportCatalogSchema>().EnableExport != EnableExport))
                return false;
            if (exportGroups.Any(g => g.GetSchema<ExportCatalogSchema>().ExportForBuildTargets != ExportForBuildTargets))
                return false;
            if (exportGroups.Any(g => g.GetSchema<ExportCatalogSchema>().BuildTargetsForCatalog.Count != BuildTargetsForCatalog.Count))
                return false;

            foreach (var target in BuildTargetsForCatalog)
                if (exportGroups.Any(g => !g.GetSchema<ExportCatalogSchema>().BuildTargetsForCatalog.Contains(target)))
                    return false;

            return true;
        }
    
    }
}
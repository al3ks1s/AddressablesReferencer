using AddressableReferencer.Editor.Analyzer;
using AddressableReferencer.Editor.Build;
using AddressableReferencer.Editor.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using static UnityEditor.PlayerSettings;
using static UnityEngine.GraphicsBuffer;

namespace AddressableReferencer.Editor.GUI
{
    public class AddressableReferencerEditorWindow : EditorWindow
    {

        [SerializeField]
        internal AddressableReferencerTreeViewState m_treeState;
        AddressableReferencerTreeView m_entryTree;

        [SerializeField]
        MultiColumnHeaderState m_mchs;

        public AddressableAssetSettings AddressableSettings
        {
            get { return AddressableAssetSettingsDefaultObject.Settings; }
        }

        public AddressableReferencerSettings Settings
        {
            get { return AddressableReferencerDefaultObject.Settings; }
        }

        private CatalogAnalyzer m_analyzer;

        private const string kStreamingAssetPath = "Select the StreamingAssets folder";
        private string m_StreamingAssetPath = kStreamingAssetPath;


        SearchField m_SearchField;
        const int k_SearchHeight = 20;

        [NonSerialized]
        List<GUIStyle> m_SearchStyles;

        [NonSerialized]
        GUIStyle m_ButtonStyle;

        [NonSerialized]
        Texture2D m_MenuIcon;

        [MenuItem("Window/Asset Management/Addressable Referencer")]
        public static void open()
        {
            var window = GetWindow<AddressableReferencerEditorWindow>("Addressables Referencer");
            window.minSize = new Vector2(430, 250);
        }

        public void OnEnable()
        {

            if (AddressableReferencerDefaultObject.SettingsExists)
            {
                if (!string.IsNullOrEmpty(AddressableReferencerDefaultObject.Settings.ExternalStreamingAssetsFolder)) { 
                    m_StreamingAssetPath = AddressableReferencerDefaultObject.Settings.ExternalStreamingAssetsFolder;

                    m_analyzer = TryGetAnalyzer();
                    if (m_analyzer == null)
                        m_StreamingAssetPath = kStreamingAssetPath;
                }
            }

            m_SearchField = new SearchField();
        
        }

        public void OnGUI()
        {
            if (m_entryTree == null)
                InitializeObjectTree();

            Rect contentRect = new Rect(0, 0, position.width, position.height);

            var inRectY = contentRect.height;
            var searchRect = new Rect(contentRect.xMin, contentRect.yMin, contentRect.width, k_SearchHeight);
            var treeRect = new Rect(contentRect.xMin, contentRect.yMin + k_SearchHeight, contentRect.width, inRectY - k_SearchHeight);

            PopulateStyles();
            RenderToolbar(searchRect);

            m_entryTree.OnGUI(treeRect);
        }

        public void RenderToolbar(Rect rect)
        {
            GUILayout.BeginArea(rect);

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            {

                GUILayout.Space(5);

                RunAnalysis();
                Tools();

                GUILayout.Label(m_StreamingAssetPath, EditorStyles.label);

                GUILayout.FlexibleSpace();

                BuildAddressableReferences();
                ClearReferences();

                GUILayout.Space(5);
                SearchBar();
                GUILayout.Space(5);

            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
        public void RunAnalysis()
        {
            var gProcessor = new GUIContent("Run", "Run Bundle Analysis");
            var gProcessorRect = GUILayoutUtility.GetRect(gProcessor, EditorStyles.toolbarDropDown);

            if (EditorGUI.DropdownButton(gProcessorRect, gProcessor, FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("Run Addressable Bundles Analysis"), false, Processbundles);
                menu.AddSeparator(string.Empty);

                menu.DropDown(gProcessorRect);
            }
        }
        public void Tools()
        {
            var gTools = new GUIContent("Tools & Setup", "Various Referencer tools and setup functions");
            var gToolsRect = GUILayoutUtility.GetRect(gTools, EditorStyles.toolbarDropDown);

            if (EditorGUI.DropdownButton(gToolsRect, gTools, FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("Set StreamingAssets Folder"), false, SelectStreamingAssetsPath);
                menu.AddItem(new GUIContent("Replace Assets"), false, ReplaceAssetReferences);
                menu.AddItem(new GUIContent("Fast Test Stuff"), false, FastTest);
                menu.AddSeparator(string.Empty);

                menu.AddItem(new GUIContent("Reset Addressables Referencer Settings"), false, ResetReferencerSetup);

                menu.DropDown(gToolsRect);
            }
        }
        public void BuildAddressableReferences()
        {
            var gBuild = new GUIContent("Build", "Call the Referencer Build Script");
            var gBuildRect = GUILayoutUtility.GetRect(gBuild, EditorStyles.toolbarDropDown);

            if (EditorGUI.DropdownButton(gBuildRect, gBuild, FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("Build Options/Copy The Catalog To The Shared Group Build Path", "Will only affect the reference script."), Settings.MoveCatalogToSharedBundleBuildPath, () => {
                    Settings.MoveCatalogToSharedBundleBuildPath = !Settings.MoveCatalogToSharedBundleBuildPath;
                });

                menu.AddItem(new GUIContent($"Build Options/Create a Catalog For Build Target/Enable All", "Ensure the game you want to reference uses the build targets you select."), false, () => {
                    foreach (var targetValue in Enum.GetValues(typeof(BuildTarget)))
                    {
                        if (BuildPipeline.IsBuildTargetSupported(EditorUserBuildSettings.selectedBuildTargetGroup, (BuildTarget)targetValue))
                        {
                            Settings.AddBuildTargetForCatalog((BuildTarget)targetValue);
                        }
                    }
                });

                foreach (var targetValue in Enum.GetValues(typeof(BuildTarget)))
                {
                    if (BuildPipeline.IsBuildTargetSupported(EditorUserBuildSettings.selectedBuildTargetGroup, (BuildTarget)targetValue)) { 
                        menu.AddItem(new GUIContent($"Build Options/Create a Catalog For Build Target/{Enum.GetName(typeof(BuildTarget), targetValue)}", "Ensure the game you want to reference uses the build targets you select."), Settings.IsBuildTargetActive((BuildTarget)targetValue), () => {
                            if (Settings.IsBuildTargetActive((BuildTarget)targetValue))
                            {
                                Settings.RemoveBuildTargetForCatalog((BuildTarget)targetValue);
                            } 
                            else
                            {
                                Settings.AddBuildTargetForCatalog((BuildTarget)targetValue);
                            }
                        });
                    }
                }

                menu.AddItem(new GUIContent($"Build Options/Create a Catalog For Build Target/Disable All", "Ensure the game you want to reference uses the build targets you select."), false, () => {
                    Settings.ClearBuildTargetForCatalogList();
                });

                menu.AddSeparator(string.Empty);
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Build Addressables Bundles With Referencer Script"), false, BuildReferenceBundles);
                
                menu.DropDown(gBuildRect);
            }
        }
        public void ClearReferences()
        {
            var gClear = new GUIContent("Clear", "Clear either reference mapping or addressable entries");
            var gClearRect = GUILayoutUtility.GetRect(gClear, EditorStyles.toolbarDropDown);

            if (EditorGUI.DropdownButton(gClearRect, gClear, FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("Clear automatic reference mapping"), false, ClearObjectMappings);
                menu.AddItem(new GUIContent("Clear manual references"), false, ClearManualMappings);
                menu.AddItem(new GUIContent("Clear reference overrides"), false, ClearMappingOverrides);
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Clear addressable groups and reference mapping"), false, ClearAddressableGroups);

                menu.DropDown(gClearRect);
            }
        }
        public void SearchBar()
        {

            Rect searchRect = GUILayoutUtility.GetRect(0, position.width * 0.6f, 16f, 16f, m_SearchStyles[0], GUILayout.MinWidth(65), GUILayout.MaxWidth(300));
            Rect popupPosition = searchRect;
            popupPosition.width = 20;

            if (Event.current.type == EventType.MouseDown && popupPosition.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Hierarchical Search"), ProjectConfigData.HierarchicalSearch, toggleHierarchicalSearch);
                menu.DropDown(popupPosition);
            }
            else
            {
                var baseSearch = ProjectConfigData.HierarchicalSearch ? m_entryTree.customSearchString : m_entryTree.searchString;
                var searchString = m_SearchField.OnGUI(searchRect, baseSearch, m_SearchStyles[0], m_SearchStyles[1], m_SearchStyles[2]);
                if (baseSearch != searchString)
                {
                    m_entryTree?.Search(searchString);
                }
            }
            //*/
        }

        // ObjectTree related stuff
        public AddressableReferencerTreeView InitializeObjectTree()
        {

            if (m_treeState == null)
                m_treeState = new AddressableReferencerTreeViewState();

            var headerState = AddressableReferencerTreeView.GetDefaultColumnState();

            if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_mchs, headerState))
                MultiColumnHeaderState.OverwriteSerializedFields(m_mchs, headerState);
            m_mchs = headerState;

            m_SearchField = new SearchField();
            m_entryTree = new AddressableReferencerTreeView(m_treeState, m_mchs, this);

            //m_entryTree.DeserializeState(AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(m_Settings)));
            //UpdateSavedColumnWidths(m_TreeState, m_Mchs);

            m_entryTree.Reload();

            return null;
        }

        private void toggleHierarchicalSearch()
        {
            ProjectConfigData.HierarchicalSearch = !ProjectConfigData.HierarchicalSearch;
            m_entryTree.SwapSearchType();
            m_entryTree.Reload();
            m_entryTree.Repaint();
        }


        private void FastTest()
        {
            Debug.Log($"{Path.GetFileName("{UnityEngine.AddressableAssets.Addressables.RuntimePath}\\StandaloneWindows64\\atlases_assets_assets/sprites/_atlases/core_glows.spriteatlas.bundle")}");
            Debug.Log($"{Path.GetFileName("{UnityEngine.AddressableAssets.Addressables.RuntimePath}/StandaloneOSX/atlases_assets_assets/sprites/_atlases/core_glows.spriteatlas.bundle")}");
            Debug.Log($"{Path.GetFileName("{UnityEngine.AddressableAssets.Addressables.RuntimePath}\\StandaloneWindows64\\atlases_assets_assets/sprites/_atlases/core_glows.spriteatlas.bundle")}");
            Debug.Log($"{Path.GetFileName("C:/Program Files (x86)/Steam/steamapps/common/Hollow Knight Silksong/Hollow Knight Silksong_Data/StreamingAssets/aa\\StandaloneWindows64\\atlases_assets_assets/sprites/_atlases/core_glows.spriteatlas.bundle")}");

            Debug.Log($"{Path.GetFileName("C:/Program Files (x86)/Steam/steamapps/common/Hollow Knight Silksong/Hollow Knight Silksong_Data/StreamingAssets/aa\\StandaloneWindows64\\sfxstatic_assets_areaorgan.bundle")}");
            Debug.Log($"{Path.GetFileName("{UnityEngine.AddressableAssets.Addressables.RuntimePath}/StandaloneOSX/sfxstatic_assets_areaorgan.bundle")}");
            Debug.Log($"{Path.GetFileName("{UnityEngine.AddressableAssets.Addressables.RuntimePath}\\StandaloneWindows64\\sfxstatic_assets_areaorgan.bundle")}");
    }


        // Processing
        private void SelectStreamingAssetsPath()
        {

            if (m_analyzer != null)
            {
                m_analyzer.UnloadCatalog();
                m_analyzer = null;
            }

            m_StreamingAssetPath = EditorUtility.OpenFolderPanel("Select StreamingAssets folder", "", "");

            if (m_StreamingAssetPath.Equals(string.Empty))
                return;

            m_StreamingAssetPath = Path.GetFullPath(m_StreamingAssetPath);

            if (!m_StreamingAssetPath.EndsWith(Path.DirectorySeparatorChar + "aa"))
                m_StreamingAssetPath = Path.Join(m_StreamingAssetPath, "aa");

            m_analyzer = TryGetAnalyzer();
            
            if (m_analyzer == null)
            {
                EditorUtility.DisplayDialog("Streaming Asset Path Error", "The provided folder does not contain a catalog, or an error occured while loading the catalog", "Ok");
                AddressableReferencerDefaultObject.Settings.ExternalStreamingAssetsFolder = string.Empty;
                m_StreamingAssetPath = kStreamingAssetPath;
                return;
            }

            AddressableReferencerDefaultObject.Settings.ExternalStreamingAssetsFolder = m_StreamingAssetPath;
            
        }
        private CatalogAnalyzer TryGetAnalyzer()
        {
            CatalogAnalyzer analyzer = new CatalogAnalyzer(m_StreamingAssetPath);

            string catalogPath = analyzer.TryFindCatalog();

            if (catalogPath.Equals(string.Empty))
            {
                m_StreamingAssetPath = "Select the StreamingAssets folder";
                return null;
            }

            if (!analyzer.LoadCatalog(catalogPath))
            {
                m_StreamingAssetPath = "Select the StreamingAssets folder";
                return null;
            }

            return analyzer;
        }
        private void Processbundles()
        {
            if (m_analyzer == null)
            {
                m_analyzer = TryGetAnalyzer();

                if (m_analyzer == null)
                {
                    EditorUtility.DisplayDialog("Streaming Asset Path Error", "Please select a folder to process before running the anlyzer", "Ok");
                    return;
                }
            }

            // Should this be done every time?
            m_analyzer.IdentifyGroups();
            m_analyzer.ProcessGroups();

            m_entryTree.Reload();

        }
        
        private void ReplaceAssetReferences()
        {
            // To implement
            
        }

        // Build
        private void BuildReferenceBundles()
        {

            string BuildScriptPath = AddressableAssetSettingsDefaultObject.Settings.DataBuilderFolder + "/" + typeof(BuildScriptReferenceMode).Name + ".asset";
            string guid = AssetDatabase.AssetPathToGUID(BuildScriptPath, AssetPathToGUIDOptions.OnlyExistingAssets);

            if (string.IsNullOrEmpty(guid))
                return;
            
            var a = AssetDatabase.LoadAssetAtPath<BuildScriptReferenceMode>(BuildScriptPath);
            var builderIndex = AddressableAssetSettingsDefaultObject.Settings.DataBuilders.IndexOf(a);

            AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilderIndex = builderIndex;

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            bool success = string.IsNullOrEmpty(result.Error);

        }

        // Clear and reset
        private void ClearAddressableGroups()
        {

            if (!EditorUtility.DisplayDialog("Reset Addressable Referencer", "Do you really want to clear all reference Addressables groups?", "Clear", "Cancel"))
            {
                return;
            }

            var groups = AddressableAssetSettingsDefaultObject.Settings.groups.Where(g => g.SchemaTypes.Contains(typeof(AddressableReferenceSchema)));
            foreach (var group in groups.ToArray())
            {
                AddressableAssetSettingsDefaultObject.Settings.RemoveGroup(group);
            }
            m_entryTree.Reload();
        }
        private void ClearObjectMappings()
        {
            
            if (!EditorUtility.DisplayDialog("Clear References mapping", "Do you really want to clear all analyzed reference mapping data?", "Clear", "Cancel"))
            {
                return;
            }

            var groups = AddressableAssetSettingsDefaultObject.Settings.groups.Where(g => g.SchemaTypes.Contains(typeof(AddressableReferenceSchema)));
            foreach (var group in groups.ToArray())
            {
                var schema = group.GetSchema(typeof(AddressableReferenceSchema)) as AddressableReferenceSchema;
                schema.Entries.Clear();
            }

            m_entryTree.Reload();
        }
        private void ClearManualMappings()
        {
            // Not implemented as of yet
        }
        private void ClearMappingOverrides()
        {
            if (!EditorUtility.DisplayDialog("Clear reference overrides", "Do you really want to clear all reference overrides?", "Clear", "Cancel"))
            {
                return;
            }
            m_entryTree.ResetOverrides();
            m_entryTree.Reload();
        }
        private void ResetReferencerSetup()
        {
            if (!EditorUtility.DisplayDialog("Reset Addressable Referencer", "Do you really want to reset all Addressable Referencer data?", "Reset", "Cancel"))
            {
                return;
            }
            AddressableReferencerDefaultObject.ClearSettings();
            AddressableReferencerDefaultObject.InitialSetup();
            m_StreamingAssetPath = kStreamingAssetPath;
            m_entryTree.Reload();
        }

        // Stylish corner
        public void PopulateStyles()
        {
            if (m_SearchStyles == null)
            {
                m_SearchStyles = new List<GUIStyle>();

                string toolbarSearchTextField = "ToolbarSeachTextFieldPopup";
                string toolbarSearchCancelButton = "ToolbarSeachCancelButton";
                string toolbarSearchCancelButtonEmpty = "ToolbarSeachCancelButtonEmpty";

                if (!HasStyle(toolbarSearchTextField))
                {
                    toolbarSearchTextField = "ToolbarSearchTextFieldPopup";
                    toolbarSearchCancelButton = "ToolbarSearchCancelButton";
                    toolbarSearchCancelButtonEmpty = "ToolbarSearchCancelButtonEmpty";
                }

                m_SearchStyles.Add(GetStyle(toolbarSearchTextField)); //GetStyle("ToolbarSearchTextField");
                m_SearchStyles.Add(GetStyle(toolbarSearchCancelButton));
                m_SearchStyles.Add(GetStyle(toolbarSearchCancelButtonEmpty));

            }

            if (m_ButtonStyle == null)
                m_ButtonStyle = GetStyle("ToolbarButton");
            if (m_MenuIcon == null)
                m_MenuIcon = EditorGUIUtility.FindTexture("_Menu");
        }
        GUIStyle GetStyle(string styleName)
        {
            GUIStyle s = UnityEngine.GUI.skin.FindStyle(styleName);
            if (s == null)
                s = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle(styleName);
            if (s == null)
            {
                Debug.LogError("Missing built-in guistyle " + styleName);
                s = new GUIStyle();
            }

            return s;
        }
        internal static bool HasStyle(string styleName)
        {
            GUIStyle s = UnityEngine.GUI.skin.FindStyle(styleName);
            if (s == null)
                s = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle(styleName);
            if (s == null)
                return false;

            return true;
        }
    }
}
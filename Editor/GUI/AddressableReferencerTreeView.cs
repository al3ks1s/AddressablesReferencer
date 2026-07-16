using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets.GUI;
using UnityEditor.AddressableAssets.GUI.Adapters;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AddressableReferencer.Editor.GUI { 
    
    public class AddressableReferencerTreeView : TreeViewAdapter
    {

        AddressableReferencerEditorWindow m_window;
        public string customSearchString = string.Empty;
        private readonly Dictionary<AddressableReferencerTreeViewItem, bool> m_SearchedEntries = new();

        enum ColumnId
        {
            Notification,
            Id,
            Object,
            PathId
        }
        ColumnId[] m_SortOptions =
        {
            ColumnId.Notification,
            ColumnId.Id,
            ColumnId.Object,
            ColumnId.PathId
        };

        GUIContent m_WarningIcon;
        GUIContent WarningIcon
        {
            get
            {
                if (m_WarningIcon == null)
                    m_WarningIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
                return m_WarningIcon;
            }
        }
        
        public AddressableReferencerTreeView(AddressableReferencerTreeViewState state, MultiColumnHeaderState mchs, AddressableReferencerEditorWindow window) : base(state, new AddressableReferencerMultiColumnHeader(mchs))
        {
            showBorder = true;
            m_window = window;
                
            columnIndexForTreeFoldouts = 1;
            multiColumnHeader.sortingChanged += OnSortingChanged;
            multiColumnHeader.columnSettingsChanged += OnColumnChanged;
            multiColumnHeader.visibleColumnsChanged += OnColumnChanged;
        }
        internal void OnSortingChanged(MultiColumnHeader mch)
        {
            if (mch is AddressableReferencerMultiColumnHeader h)
            {
                h.SaveEditorPrefs();
            }
            Reload();
        }
        internal void OnColumnChanged(MultiColumnHeader mch)
        {
            if (mch is AddressableReferencerMultiColumnHeader h)
            {
                h.SaveEditorPrefs();
            }
        }
        internal void OnColumnChanged(int columnIndex)
        {
            if (multiColumnHeader is AddressableReferencerMultiColumnHeader h)
            {
                h.SaveEditorPrefs();
            }
        }

        // Build tree
        internal TreeViewItemAdapter Root => rootItem as TreeViewItemAdapter;
        protected override TreeViewItemAdapter BuildRootAdapter()
        {
            var root = new TreeViewItemAdapter(-1, -1);

            SortGroups();
            var guidMap = new Dictionary<string, AddressableAssetGroup>();
            foreach (var group in m_window.AddressableSettings.groups)
            {
                if (group == null)
                    continue;
                guidMap.Add(group.Guid, group);
            }

            // Manual object mapping in case it is needed
            string manualMapString = "Manual object references";
            root.AddChild(new AddressableReferencerTreeViewItem(manualMapString));

            foreach (var groupGuid in GetTreeViewState().sortOrderList)
                AddGroupChildrenBuild(guidMap[groupGuid], root);

            return root;
        }
        protected override IList<TreeViewItemAdapter> BuildRowsAdapter(TreeViewItemAdapter root)
        {
            // m_lazyLoader.ClearWorkQueue();
            if (!string.IsNullOrEmpty(searchString))
            {
                var rows = base.BuildRowsAdapter(root);

                // At the end of a Search, we schedule an icon lazy load only for results
                // By doing this, we ensure we only load necessary icons, which massively improves search performance
                rows = Search(rows);

                var result = new List<TreeViewItemAdapter>();
                foreach (var node in SortHierarchical(rows))
                    result.Add(node as TreeViewItemAdapter);
                return result;
            }

            if (!string.IsNullOrEmpty(customSearchString))
            {
                SortChildren(root);
                return Search(base.BuildRowsAdapter(root));
            }

            SortChildren(root);
            //LazyLoadIcons(root);
            return base.BuildRowsAdapter(root);
        }
        public void AddGroupChildrenBuild(AddressableAssetGroup group, TreeViewItemAdapter root)
        {

            AddressableReferencerTreeViewItem groupItem = null;

            if (!group.IsReferenceGroup())
                return;

            groupItem = new AddressableReferencerTreeViewItem(group);
            root.AddChild(groupItem);

            AddressableReferenceSchema referenceSchema = group.GetSchema<AddressableReferenceSchema>();

            if (group != null && referenceSchema.EntryCount > 0)
            {
                foreach (var entry in referenceSchema.Entries)
                {
                    AddandRecurseReferenceEntries(entry, groupItem, IsExpanded(groupItem.id));
                }
            }

        }
        public void AddandRecurseReferenceEntries(AddressableReferenceEntry entry, AddressableReferencerTreeViewItem group, bool isExpanded)
        {
            var item = new AddressableReferencerTreeViewItem(entry);
            group.AddChild(item);

            RecurseMappingObjects(entry, item);
        }
        public void RecurseMappingObjects(AddressableReferenceEntry entry, AddressableReferencerTreeViewItem item)
        {
            foreach (var mapping in entry.m_ObjectMapping)
            {
                item.AddChild(new AddressableReferencerTreeViewItem(mapping, 2));
            }
        }
        public void ManualMapping(TreeViewItemAdapter root)
        {
            
        }

        // Gui operations
        public override void OnGUI(Rect rect)
        {
            base.OnGUI(rect);            
        }
        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item as AddressableReferencerTreeViewItem;
            bool isReadOnly = item.group == null ? false : item.group.ReadOnly;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                CellGUI(args.GetCellRect(i), item, args.GetColumn(i), ref args);

        }
        private void CellGUI(Rect cellRect, AddressableReferencerTreeViewItem item, int column, ref RowGUIArgs args)
        {
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch ((ColumnId)column)
            {
                case ColumnId.Notification:
                    if (item.HasOverride) {

                        var notification = WarningIcon;

                        if (item.type == AddressableReferencerTreeViewItem.ItemType.Object) 
                        { 
                            notification.tooltip = "This object's Path Id is being overridden. " +
                                "Ensure the Path Id override is correct for the asset in-game.";
                        }
                        else
                        {
                            notification.tooltip = "One or more of the objects in that group/bundle has a PathId override. " +
                                "Ensure the Path Id override is correct for the asset in-game.";
                        }

                        UnityEngine.GUI.Label(cellRect, notification);
                    }
                    break;

                case ColumnId.Id:
                    args.rowRect = cellRect;
                    base.RowGUI(args);
                    break;

                case ColumnId.Object:

                    if (item.type == AddressableReferencerTreeViewItem.ItemType.Object)
                    {
                        
                        // Add a dropfown for manual maps for chosing the object / Component
                        using (new EditorGUI.DisabledScope(true))
                        {
                            string assetPath = AssetDatabase.GUIDToAssetPath(item.mappedObject.m_GUID);
                            UnityEngine.Object obj = ObjectIdentifier.ToObject(item.mappedObject.ObjectId);
                            EditorGUI.ObjectField(cellRect, obj, obj.GetType(), false); 
                        }
                    }

                    break;
                case ColumnId.PathId:

                    if (item.type == AddressableReferencerTreeViewItem.ItemType.Object)
                    {

                        long pathId = 0;
                        var pathIdField = item.mappedObject.Overridden ? item.mappedObject.m_pathIdOverride : item.mappedObject.m_pathId;
                        pathId = EditorGUI.LongField(cellRect, pathIdField); 

                        if (pathId != 0)
                        {
                            if (pathId != item.mappedObject.m_pathId)
                            {
                                item.mappedObject.m_pathIdOverride = pathId;
                            }
                        } 
                        else
                        {
                            item.mappedObject.ResetOverride();
                        }
                    
                    }

                    break;


            }



        }



        // Searching
        internal IList<TreeViewItemAdapter> Search(string search)
        {
            if (ProjectConfigData.HierarchicalSearch)
            {
                customSearchString = search;
                Reload();
            }
            else
            {
                searchString = search;
            }

            var rows = GetRows();
            var result = new List<TreeViewItemAdapter>();
            foreach (var node in rows)
            {
                result.Add(node as TreeViewItemAdapter);
            }
            return result;
        }
        protected IList<TreeViewItemAdapter> Search(IList<TreeViewItemAdapter> rows)
        {
            if (rows == null)
                return new List<TreeViewItemAdapter>();

            m_SearchedEntries.Clear();
            List<TreeViewItemAdapter> items = new List<TreeViewItemAdapter>(rows.Count);
            foreach (TreeViewItemAdapter item in rows)
            {
                if (ProjectConfigData.HierarchicalSearch)
                {
                    if (SearchHierarchical(item, customSearchString))
                        items.Add(item);
                }
                else if (DoesItemMatchSearch(item, searchString))
                    items.Add(item);
            }
           
            return items;
        }
        bool SearchHierarchical(TreeViewItemAdapter item, string search, bool? ancestorMatching = null)
        {
            var aeItem = item as AddressableReferencerTreeViewItem;
            if (aeItem == null || search == null)
                return false;

            if (m_SearchedEntries.ContainsKey(aeItem))
                return m_SearchedEntries[aeItem];

            if (ancestorMatching == null)
                ancestorMatching = DoesAncestorMatch(aeItem, search);

            bool isMatching = false;
            if (!ancestorMatching.Value)
                isMatching = DoesItemMatchSearch(aeItem, search);

            bool descendantMatching = false;
            if (!ancestorMatching.Value && !isMatching && aeItem.hasChildren)
            {
                foreach (var child in aeItem.children)
                {
                    descendantMatching = SearchHierarchical(child as TreeViewItemAdapter, search, false);
                    if (descendantMatching)
                        break;
                }
            }

            bool keep = isMatching || ancestorMatching.Value || descendantMatching;
            m_SearchedEntries.Add(aeItem, keep);
            return keep;
        }
        private bool DoesAncestorMatch(TreeViewItemAdapter aeItem, string search)
        {
            if (aeItem == null)
                return false;

            var ancestor = aeItem.parent as AddressableReferencerTreeViewItem;
            bool isMatching = DoesItemMatchSearch(ancestor, search);
            while (ancestor != null && !isMatching)
            {
                ancestor = ancestor.parent as AddressableReferencerTreeViewItem;
                isMatching = DoesItemMatchSearch(ancestor, search);
            }

            return isMatching;
        }
        internal void SwapSearchType()
        {
            string temp = customSearchString;
            customSearchString = searchString;
            searchString = temp;
            m_SearchedEntries.Clear();
        }
        protected override bool DoesItemMatchSearch(TreeViewItemAdapter item, string search)
        {
            if (string.IsNullOrEmpty(search))
                return true;

            var aeItem = item as AddressableReferencerTreeViewItem;
            if (aeItem == null)
                return false;

            //check if item matches.
            if (aeItem.displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (aeItem.type == AddressableReferencerTreeViewItem.ItemType.Object)
            {
                long.TryParse(search, out var pathid);
                if (aeItem.mappedObject.m_pathId == pathid)
                    return true;
            }

            return false;
        }

        // Click operations
        bool m_ContextOnItem;
        protected override void ContextClicked()
        {
            if (m_ContextOnItem)
            {
                m_ContextOnItem = false;
                return;
            }

            GenericMenu menu = new GenericMenu();
            PopulateGeneralContextMenu(ref menu);
            menu.ShowAsContext();
        }
        void PopulateGeneralContextMenu(ref GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Create New Object Mapping"), false, CreateMapping);
        }
        protected override void ContextClickedItem(int id)
        {
            List<AddressableReferencerTreeViewItem> selectedNodes = new List<AddressableReferencerTreeViewItem>();
            foreach (var nodeId in GetSelection())
            {
                var item = FindItemInVisibleRows(nodeId); //TODO - this probably makes off-screen but selected items not get added to list.
                if (item != null)
                    selectedNodes.Add(item);
            }

            if (selectedNodes.Count == 0)
                return;

            m_ContextOnItem = true;

            bool isGroup = false;
            bool isBundle = false;
            bool isObject = false;
            bool hasReadOnly = false;
            bool isMissingPath = false;
            foreach (var item in selectedNodes)
            {
                if (item.type == AddressableReferencerTreeViewItem.ItemType.Group)
                {
                    isGroup = true;
                }
                else if (item.type == AddressableReferencerTreeViewItem.ItemType.Bundle)
                {
                    isBundle = true;
                }
                else if (item.type == AddressableReferencerTreeViewItem.ItemType.Object)
                {
                    isObject = true;
                }
            }

            if (isBundle && isGroup)
                return;

            GenericMenu menu = new GenericMenu();
            if (selectedNodes.Count == 1)
            {

            }
            if (!hasReadOnly)
            {
                if (isGroup)
                {

                    var item = selectedNodes.First();

                    if (selectedNodes.Count == 1)
                    {
                        if (item.type == AddressableReferencerTreeViewItem.ItemType.Group && item.isManualMap)
                        {
                            PopulateGeneralContextMenu(ref menu);
                        }
                    }

                    //var group = selectedNodes.First().group;
                    //menu.AddItem(new GUIContent("Simplify Addressable Names"), false, SimplifyAddresses, selectedNodes);
                    //if (selectedNodes.Count == 1)
                    //{
                    //    if (!group.IsDefaultGroup() && group.CanBeSetAsDefault())
                    //        menu.AddItem(new GUIContent("Set as Default"), false, SetGroupAsDefault, selectedNodes);
                    //}
                    //menu.AddItem(new GUIContent("Convert schema(s) to Content Directory"), false, ConvertToContentDirectory, selectedNodes);

                    //if (!group.IsDefaultGroup())
                    //    menu.AddItem(new GUIContent("Delete Group(s)"), false, RemoveGroup, selectedNodes);

                    //foreach (var i in AddressableAssetSettings.CustomAssetGroupCommands)
                    //    menu.AddItem(new GUIContent(i), false, HandleCustomContextMenuItemGroups, new Tuple<string, List<AssetEntryTreeViewItem>>(i, selectedNodes));
                }
                else if (isBundle)
                {
                    //    menu.AddItem(new GUIContent("Move Addressables to Group..."), false, MoveEntriesToGroup, new Tuple<Event, List<AssetEntryTreeViewItem>>(Event.current, selectedNodes));
                    //    menu.AddItem(new GUIContent("Move Addressables to New Group with settings from..."), false, MoveEntriesToNewGroupWithSettings, new Tuple<Event, List<AssetEntryTreeViewItem>>(Event.current, selectedNodes));

                    //    menu.AddItem(new GUIContent("Remove Addressables"), false, RemoveEntry, selectedNodes);
                    //    menu.AddItem(new GUIContent("Simplify Addressable Names"), false, SimplifyAddresses, selectedNodes);

                    //    if (selectedNodes.Count == 1)
                    //        menu.AddItem(new GUIContent("Copy Address to Clipboard"), false, CopyAddressesToClipboard, selectedNodes);

                    //    else if (selectedNodes.Count > 1)
                    //        menu.AddItem(new GUIContent("Copy " + selectedNodes.Count + " Addresses to Clipboard"), false, CopyAddressesToClipboard, selectedNodes);

                    //    foreach (var i in AddressableAssetSettings.CustomAssetEntryCommands)
                    //        menu.AddItem(new GUIContent(i), false, HandleCustomContextMenuItemEntries, new Tuple<string, List<AssetEntryTreeViewItem>>(i, selectedNodes));
                    //}
                    //else
                    //    menu.AddItem(new GUIContent("Clear missing references."), false, RemoveMissingReferences);
                }
                else
                {
                    //if (isObject)
                    //{
                    //    if (selectedNodes.Count == 1)
                    //        menu.AddItem(new GUIContent("Copy Address to Clipboard"), false, CopyAddressesToClipboard, selectedNodes);
                    //    else if (selectedNodes.Count > 1)
                    //        menu.AddItem(new GUIContent("Copy " + selectedNodes.Count + " Addresses to Clipboard"), false, CopyAddressesToClipboard, selectedNodes);
                    //}
                }

                menu.ShowAsContext();
            }
        }
        AddressableReferencerTreeViewItem FindItemInVisibleRows(int id)
        {
            var rows = GetRows();
            foreach (var r in rows)
            {
                if (r.id == id)
                {
                    return r as AddressableReferencerTreeViewItem;
                }
            }

            return null;
        }
        protected override void DoubleClickedItem(int id)
        {
            var item = FindItemInVisibleRows(id);
            if (item != null)
            {
                UnityEngine.Object o = null;
                if (item.type == AddressableReferencerTreeViewItem.ItemType.Object)
                    o = ObjectIdentifier.ToObject(item.mappedObject.ObjectId);
                else if (item.type == AddressableReferencerTreeViewItem.ItemType.Group)
                    o = item.group;

                if (o != null)
                {
                    EditorGUIUtility.PingObject(o);
                    Selection.activeObject = o;
                }
            }
        }

        // Sorting
        void SortChildren(TreeViewItemAdapter root)
        {
            if (!root.hasChildren)
                return;

            foreach (var child in root.children)
            {
                if (child != null && IsExpanded(child.id))
                {
                    child.children = SortHierarchical(child.children);
                }
            }
        }
        List<TreeViewItem> SortHierarchical(IList<TreeViewItemAdapter> children)
        {
            var result = new List<TreeViewItem>();
            foreach (var c in children)
            {
                result.Add(c as TreeViewItem);
            }
            return SortHierarchical(result);
        }
        List<TreeViewItem> SortHierarchical(List<TreeViewItem> children)
        {
            var result = new List<TreeViewItem>();

            if (children == null)
                return children;

            var sortedColumns = multiColumnHeader.state.sortedColumns;
            if (sortedColumns.Length == 0)
                return children;

            List<AddressableReferencerTreeViewItem> kids = new List<AddressableReferencerTreeViewItem>();
            foreach (var c in children)
            {
                var child = c as AddressableReferencerTreeViewItem;
                if (child != null && child.type == AddressableReferencerTreeViewItem.ItemType.Object)
                    kids.Add(child);
                else
                    result.Add(c);
            }

            ColumnId col = m_SortOptions[sortedColumns[0]];
            bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[0]);

            IEnumerable<AddressableReferencerTreeViewItem> orderedKids = kids;
            switch (col)
            {
                case ColumnId.Notification:
                    break;
                case ColumnId.Id:
                    orderedKids = kids.Order(l => l.displayName, ascending);
                    break;
                case ColumnId.PathId:
                    orderedKids = kids.Order(l => l.mappedObject.m_pathId.ToString(), ascending);
                    break;
                default:
                    orderedKids = kids.Order(l => l.displayName, ascending);
                    break;
            }

            foreach (var o in orderedKids)
                result.Add(o);

            foreach (var child in result)
            {
                if (child != null && IsExpanded(child.id))
                    child.children = SortHierarchical(child.children);
            }
            return result;
        }

        // Other stuff
        public static MultiColumnHeaderState GetDefaultColumnState()
        {
            return new MultiColumnHeaderState(AddressableReferencerMultiColumnHeader.GetColumns());
        }
        internal void SortGroups()
        {
            if (state is AddressableAssetEntryTreeViewState s)
            {
                var missingGuid = false;
                var guidToName = new Dictionary<string, string>();
                var newSortOrder = new List<string>();
                var guidToExistingIndex = new Dictionary<string, int>();
                for (var i = 0; i < s.sortOrderList.Count; i++)
                {
                    guidToExistingIndex[s.sortOrderList[i]] = i;
                }
                for (var i = 0; i < m_window.AddressableSettings.groups.Count; i++)
                {
                    var group = m_window.AddressableSettings.groups[i];
                    if (group == null)
                        continue;

                    var guid = group.Guid;
                    newSortOrder.Add(guid);
                    guidToName[guid] = m_window.AddressableSettings.groups[i].Name;
                    if (!guidToExistingIndex.ContainsKey(guid))
                    {
                        missingGuid = true;
                        guidToExistingIndex[guid] = -1;
                    }
                }

                // if the count is the same and all of the guids are in the state's sortOrder skip sorting
                if (m_window.AddressableSettings.groups.Count == s.sortOrderList.Count && !missingGuid)
                {
                    return;
                }

                // Rules:
                // if both have indexes we compare by index
                // if one doesn't have an index we want it at the top so we set the default index to -1
                // if both don't have indexes we want to compare them alphabetically
                newSortOrder.Sort((guid1, guid2) =>
                {
                    // assign -1 by default to push to the top of the list if they don't appear
                    // in the older sorting
                    var index1 = guidToExistingIndex[guid1];
                    var index2 = guidToExistingIndex[guid2];
                    // neither of these elements appear in the older sorting
                    if (index1 == -1 && index2 == -1)
                    {
                        // compare alphabetically
                        return string.CompareOrdinal(guidToName[guid1], guidToName[guid2]);
                    }
                    // compare by index in the previous sorting
                    return index1
                        .CompareTo(index2);
                });
                s.sortOrderList = newSortOrder;

                // we have updated the sort order, so save it
                SerializeState(AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(m_window.AddressableSettings)));
            }

        }
        public void SerializeState(GUID guid)
        {

            if (state is AddressableAssetEntryTreeViewState s)
            {
                var settings = AddressableAssetGroupSortSettings.GetSettings();

                bool hasChanged = settings.sortOrder == null || settings.sortOrder.Length != s.sortOrderList.Count;
                if (!hasChanged)
                {
                    for (var i = 0; i < s.sortOrderList.Count; i++)
                    {
                        if (settings.sortOrder[i] != s.sortOrderList[i])
                        {
                            hasChanged = true;
                            break;
                        }
                    }
                }

                // Only update and save if something actually changed
                if (hasChanged)
                {
                    settings.sortOrder = s.sortOrderList.ToArray();
                    EditorUtility.SetDirty(settings);
                }
            }

        }
        public void DeserializeState(GUID guid)
        {
            if (state is AddressableAssetEntryTreeViewState s)
            {
                var settings = AddressableAssetGroupSortSettings.GetSettings();
                s.sortOrderList = new List<string>();
                s.sortOrderList.AddRange(settings.sortOrder);
            }
        }
        public AddressableAssetEntryTreeViewState GetTreeViewState()
        {
            var s = state as AddressableAssetEntryTreeViewState;
            if (s == null)
            {
                throw new Exception("GroupTreeView using incompatible state " + state.GetType());
            }
            return s;
        }

        // Content operations
        public void ResetOverrides()
        {
            Root.children.ForEach(
                group => {
                    if (group.hasChildren)
                        group.children.ForEach(
                            bundle => {
                                if (bundle.hasChildren)
                                    bundle.children.ForEach(
                                        map =>
                                        {
                                            ((AddressableReferencerTreeViewItem)map).mappedObject.ResetOverride();
                                        }
                                    );
                            }
                        );
                }
            );
        }
        public void CreateMapping()
        {

        }

    }

    public class AddressableReferencerTreeViewItem : TreeViewItemAdapter
    {

        public enum ItemType
        {
            Group,
            Bundle,
            Object
        };

        public ItemType type;

        public AddressableAssetGroup group;

        public AddressableReferenceEntry bundleEntry;
        public ObjectMapping mappedObject;

        public Texture2D assetIcon;

        public bool isManualMap;

        public AddressableReferencerTreeViewItem(string s) : base(s == null ? 0 : s.GetHashCode(), 0, s == null ? "[Missing Reference]" : s)
        {
            group = null;

            bundleEntry = null;
            mappedObject = null;
            assetIcon = null;

            isManualMap = true;

            type = ItemType.Group;
        }

        public AddressableReferencerTreeViewItem(AddressableAssetGroup g) : base(g == null ? 0 : g.Guid.GetHashCode(), 0, g == null ? "[Missing Reference]" : g.Name) 
        {
            group = g;

            bundleEntry = null;
            mappedObject = null;

            assetIcon = null;

            type = ItemType.Group;
        }

        public AddressableReferencerTreeViewItem(AddressableReferenceEntry e) : base(e == null ? 0 : e.cabName.GetHashCode(), 1, e == null ? "[Missing Reference]" : Path.GetFileNameWithoutExtension(e.baseInternalId)) 
        {
            group = null;

            bundleEntry = e;
            mappedObject = null;
            assetIcon = null;

            type = ItemType.Bundle;
        }

        public AddressableReferencerTreeViewItem(ObjectMapping map, int d) : base(map == null ? 0 : map.GetHashCode(), d, map == null ? "[Missing Reference]" : AssetDatabase.GUIDToAssetPath(map.m_GUID)) 
        {
            group = null;

            bundleEntry = null;
            mappedObject = map;
            assetIcon = null;

            type = ItemType.Object;
        }

        public bool HasOverride
        {
            get 
            {
                if (type == ItemType.Object)
                    return mappedObject.Overridden;

                if (!hasChildren)
                    return false;

                if (type == ItemType.Group || type == ItemType.Bundle)
                    return children.Any(mpo => ((AddressableReferencerTreeViewItem)mpo).HasOverride);
                
                return false;
            }
        }
    }

    // Taken directly from addressables
    static class MyExtensionMethods
    {
        // Find digits in a string
        static Regex s_Regex = new Regex(@"\d+", RegexOptions.Compiled);

        public static IEnumerable<T> Order<T>(this IEnumerable<T> items, Func<T, string> selector, bool ascending)
        {
            if (EditorPrefs.HasKey("AllowAlphaNumericHierarchy") && EditorPrefs.GetBool("AllowAlphaNumericHierarchy"))
            {
                // Find the length of the longest number in the string
                int maxDigits = items
                    .SelectMany(i => s_Regex.Matches(selector(i)).Cast<Match>().Select(digitChunk => (int?)digitChunk.Value.Length))
                    .Max() ?? 0;

                // in the evaluator, pad numbers with zeros so they all have the same length
                var tempSelector = selector;
                selector = i => s_Regex.Replace(tempSelector(i), match => match.Value.PadLeft(maxDigits, '0'));
            }

            return ascending ? items.OrderBy(selector) : items.OrderByDescending(selector);
        }
    }
}

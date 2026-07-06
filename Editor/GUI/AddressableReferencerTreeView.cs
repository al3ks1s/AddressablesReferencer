using UnityEditor.AddressableAssets.GUI.Adapters;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AddressableReferencer.Editor.GUI { 
    
    public class AddressableReferencerTreeView : TreeViewAdapter
    {
        
        public AddressableReferencerTreeView(AddressableReferencerTreeViewState state, MultiColumnHeaderState mchs): base(state, new AddressableReferencerMultiColumnHeader(mchs))
            { }

        protected override TreeViewItemAdapter BuildRootAdapter()
        {
            return new();
        }


    }


    public class AddressableReferencerTreeViewItem : TreeViewItemAdapter
    {
        AddressableAssetEntry entry;
        AddressableAssetGroup group;

        AddressableReferenceEntry bundleEntry;
        ObjectMapping mappedObject;

        public AddressableReferencerTreeViewItem() : base() { }

    }

}

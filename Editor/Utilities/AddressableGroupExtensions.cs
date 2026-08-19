using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace AddressableReferencer.Editor.Utilities
{
    public static class AddressableGroupExtensions
    {

        public static bool IsReferenceGroup(this AddressableAssetGroup group)
        {
            return group != null && group.HasSchema<AddressableReferenceSchema>();
        }

    }
}
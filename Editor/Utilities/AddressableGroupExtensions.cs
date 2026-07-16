using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class AddressableGroupExtensions
{

    public static bool IsReferenceGroup(this AddressableAssetGroup group)
    {
        return group.GetSchema<AddressableReferenceSchema>() != null;
    }

}

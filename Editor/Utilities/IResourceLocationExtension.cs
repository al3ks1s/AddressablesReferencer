using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;

public static class IResourceLocationExtension
{

    public static string ReverseBundleInternalId(this IResourceLocation location)
    {
        string internalId = location.InternalId;
        internalId = internalId.Replace(UnityEngine.AddressableAssets.Addressables.RuntimePath, "{UnityEngine.AddressableAssets.Addressables.RuntimePath}");

        BuildTarget[] targets = (BuildTarget[])Enum.GetValues(typeof(BuildTarget));
        Array.Sort(targets, (x, y) => Enum.GetName(typeof(BuildTarget), y).Length.CompareTo(Enum.GetName(typeof(BuildTarget), x).Length));

        foreach (var target in targets)
        {
            if (internalId.Contains(Enum.GetName(typeof(BuildTarget), (BuildTarget)target)))
            {
                internalId = internalId.Replace(Enum.GetName(typeof(BuildTarget), (BuildTarget)target), "{BuildTarget}").Replace("\\", "/");
            }
        }
        
        return internalId;
    }

    internal static char PathSeparatorForPlatform(this BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows64:
            case BuildTarget.StandaloneWindows:
            case BuildTarget.XboxOne:
                return '\\';
            case BuildTarget.GameCoreXboxOne:
                return '\\';
            case BuildTarget.Android:
                return '/';
            default:
                return '/';
        }
    }

}

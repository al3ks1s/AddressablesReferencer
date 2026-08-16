using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;

public static class IResourceLocationExtension
{

    /// <summary>
    /// Constructs a <see cref="BuildTarget"/>-agnostic InternalId from the Addressables Location.
    /// This internal id will then be processed during the build script to fit the targeted platform as the <see cref="BuildTarget"/> cannot be inferred at runtime.
    /// </summary>
    /// <param name="location">The <see cref="IResourceLocation"/> associated to an addressable bundle.</param>
    /// <returns>The newly created bundle path of the Location</returns>
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

    /// <summary>
    /// Reconstructs the Internal Id (Bundle path) of the given resource location.
    /// This is needed as loading the game's catalog in-editor will process the Addressables Runtime Path.
    /// </summary>
    /// <param name="location">The <see cref="IResourceLocation"/> associated to an addressable bundle.</param>
    /// <param name="StreamingAssetsPath">The <see cref="Path"/> of the StreamingAssets/aa folder for the game being processed</param>
    /// <returns></returns>
    public static string GetFullInternalIdPath(this IResourceLocation location, string StreamingAssetsPath)
    {
        var bundlePath = location.InternalId.Replace(UnityEngine.AddressableAssets.Addressables.RuntimePath, "");
        bundlePath = Path.Join(StreamingAssetsPath, bundlePath);
        bundlePath = bundlePath.Replace("\\", "/");
        bundlePath = Path.GetFullPath(bundlePath);

        return bundlePath;
    }

    /// <summary>
    /// Retrieves the path separator for the build target.
    /// </summary>
    /// <param name="target">The <see cref="BuildTarget"/> fow which the Addressables References is being built</param>
    /// <returns>The single path separator character</returns>
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

    
    internal static string GetBuiltInBundleName(AddressableAssetsBuildContext aaContext)
    {
        return GetBuiltInBundleNamePrefix(aaContext) + $"{BuildScriptBase.BuiltInBundleBaseName}.bundle";
    }
    internal static string GetBuiltInBundleNamePrefix(AddressableAssetsBuildContext aaContext)
    {
        return GetBuiltInBundleNamePrefix(aaContext.Settings);
    }
    internal static string GetBuiltInBundleNamePrefix(AddressableAssetSettings settings)
    {
        string value = "";
        switch (settings.BuiltInBundleNaming)
        {
            case BuiltInBundleNaming.DefaultGroupGuid:
                value = settings.DefaultGroup.Guid;
                break;
            case BuiltInBundleNaming.ProjectName:
                value = UnityEngine.Hash128.Compute(GetProjectName()).ToString();
                break;
            case BuiltInBundleNaming.Custom:
                value = settings.BuiltInBundleCustomNaming;
                break;
        }

        return value;
    }

    internal static string GetMonoScriptBundleName(AddressableAssetsBuildContext aaContext)
    {
        return GetMonoScriptBundleNamePrefix(aaContext) + "_monoscripts.bundle";
    }
    internal static string GetMonoScriptBundleNamePrefix(AddressableAssetsBuildContext aaContext)
    {
        return GetMonoScriptBundleNamePrefix(aaContext.Settings);
    }
    internal static string GetMonoScriptBundleNamePrefix(AddressableAssetSettings settings)
    {
        string value = null;
        switch (settings.MonoScriptBundleNaming)
        {
            case MonoScriptBundleNaming.ProjectName:
                value = Hash128.Compute(GetProjectName()).ToString();
                break;
            case MonoScriptBundleNaming.DefaultGroupGuid:
                value = settings.DefaultGroup.Guid;
                break;
            case MonoScriptBundleNaming.Custom:
                value = settings.MonoScriptBundleCustomNaming;
                break;
        }

        return value;
    }
    
    internal static string GetProjectName()
    {
        return new DirectoryInfo(Path.GetDirectoryName(Application.dataPath)).Name;
    }

}

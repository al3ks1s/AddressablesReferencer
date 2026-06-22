using AddressableReferencer.Editor.Analyzer;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using static UnityEditor.AddressableAssets.Build.Layout.BuildLayout;
using static UnityEditor.FilePathAttribute;

public class FolderAnalyzer : GenericAnalyzer
{
    public FolderAnalyzer(BundleAnalyzer parentAnalyzer) : base(parentAnalyzer)
    { }

    public override (AddressableAssetEntry, List<ObjectMapping>) Analyze(long _, string __)
    {
        var bundleBase = AssetManager.GetBaseField(CabFile, AssetBundle);

        string bundleName = Path.GetFileName(Location.InternalId);
        string folderPath = String.Empty;

        List<ObjectMapping> objectMaps = new List<ObjectMapping>();

        foreach (var asset in bundleBase["m_Container.Array"])
        {
            string path = asset["first"].AsString;
            long pathId = asset["second.asset.m_PathID"].AsLong;

            var assetExt = AssetManager.GetExtAsset(CabFile, 0, pathId);
            var analyzer = GenericAnalyzer.GetAnalyzer(assetExt.baseField.TypeName, m_parentAnalyzer);
            var kvp = analyzer.Analyze(pathId, path);
                       
            folderPath = getCommonPath(folderPath, path);
            // Means the bundle addresses a folder but has only a single asset in it. I love reverse engineering stuff. Maybe its unneccessary?
            if (bundleName.Split(".").Length == 2 && AssetCount == 1)
                folderPath = getFolderPath(path, bundleName);

            if (kvp.Item1 != null)
                AddressableAssetSettingsDefaultObject.Settings.RemoveAssetEntry(kvp.Item1.guid);

            if (kvp.Item2 != null)  
                objectMaps.AddRange(kvp.Item2);

        }

        var folderGUID = AssetDatabase.AssetPathToGUID(folderPath, AssetPathToGUIDOptions.OnlyExistingAssets);

        if (!folderGUID.Equals(string.Empty))
            return (CreateOrGetAssetEntry(folderGUID), objectMaps);

        return (null, null);
    
    }

    private string getFolderPath(string path, string bundleName)
    {

        while (!Path.GetFileNameWithoutExtension(path).ToLower().Replace(" ", "").Equals(Path.GetFileNameWithoutExtension(bundleName)))
        {
            path = Path.GetDirectoryName(path);
        }

        return path;

    }

    private string getCommonPath(string first, string second)
    {

        if (first.Equals(string.Empty))
            return second;

        if (second.Equals(string.Empty))
            return string.Empty;


        var lf = first.Split("/");
        var ls = second.Split("/");

        int minL = Math.Min(lf.Length, ls.Length);

        string commonPath = "Assets";

        for (int i = 1; i < minL; i++)
        {
            if (lf[i].Equals(ls[i]))
            {
                commonPath = $"{commonPath}/{lf[i]}";
            }
        }

        return commonPath.Trim('/');


    }

}

using AssetHelperLib.BundleTools;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.U2D;
using UnityEngine.UIElements;

public class BundleAnalyzer
{

    string StreamingAssetsPath;

    AssetBundle loadedBundle;

    IResourceLocation location;
    AddressableAssetGroup assetGroup;
    BundledAssetGroupSchema schema;
    AddressableReferenceSchema referenceSchema;
    AddressableReferenceEntry referenceEntry;

    AssetsManager mgr;
    BundleFileInstance bundle;
    AssetsFileInstance CABFile;
    AssetFileInfo assetBundle;


    // Monoscript bundle lookup to get the MonoBehaviour underlying types
    IResourceLocation monoscriptLocation;
    BundleFileInstance monoscriptBundle;
    AssetsFileInstance monoscriptFile;

    // Processing variables
    Dictionary<string, int> pathCounts = new();
    Dictionary<string, ObjectIdentifier[]> assetRepresentations = new();

    HashSet<string> alreadySeenPaths = new();

    List<AddressableAssetEntry> entries = new();

    private bool IsLoaded { get; set; } = false;

    private bool IsFolderBundle { 
        get {

            if (!IsLoaded)
                return false;

             return schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
        }
    }

    private int AssetCount
    {
        get
        {
            if (!IsLoaded)
                return 0;

            var bundleBase = mgr.GetBaseField(CABFile, assetBundle);
            var assetCount = bundleBase["m_Container.Array"].AsArray.size;
            
            return assetCount;
        }
    }

    public string ResolveBundlePath(IResourceLocation bundle)
    {
        var bundlePath = bundle.InternalId.Replace(UnityEngine.AddressableAssets.Addressables.RuntimePath, "");
        return Path.Join(StreamingAssetsPath, bundlePath);
    }

    public BundleAnalyzer(IResourceLocation loc, AddressableAssetGroup grp, string streamingAssetPath, IResourceLocation monoscript = null)
    {
        mgr = BundleUtils.CreateDefaultManager();

        location = loc;
        assetGroup = grp;
        StreamingAssetsPath = streamingAssetPath;

        schema = (BundledAssetGroupSchema)assetGroup.Schemas.Find(s => s is BundledAssetGroupSchema);
        referenceSchema = (AddressableReferenceSchema)assetGroup.Schemas.Find(s => s is AddressableReferenceSchema);

        referenceEntry = new();
        referenceSchema.Entries.Add(referenceEntry);

        if (monoscript != null)
        {
            monoscriptLocation = monoscript;
            LoadMonoscript();
        }

        LoadBundle();

        IsLoaded = true;
    }
    
    public void LoadBundle()
    {

        bundle = mgr.LoadBundleFile(ResolveBundlePath(location));
        CABFile = mgr.LoadAssetsFileFromBundle(bundle, 0, false);

        assetBundle = CABFile.file.GetAssetsOfType(AssetClassID.AssetBundle).First();

        var bundleBase = mgr.GetBaseField(CABFile, assetBundle);

        loadedBundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault<AssetBundle>(b => b.name == bundleBase["m_Name"].AsString);
        if (loadedBundle == null)
        {
            loadedBundle = AssetBundle.LoadFromFile(ResolveBundlePath(location));   
        }

    }

    public void LoadMonoscript()
    {
        monoscriptBundle = mgr.LoadBundleFile(ResolveBundlePath(monoscriptLocation));
        monoscriptFile = mgr.LoadAssetsFileFromBundle(monoscriptBundle, 0, false);
    }


    // Actual processing
    public void ProcessBundle()
    {
        var bundleBase = mgr.GetBaseField(CABFile, assetBundle);
        bool isScene = bundleBase["m_IsStreamedSceneAssetBundle"].AsBool;

        if (isScene)
        {
            return;
        }

        SearchContainerPaths();
        GenerateBundleName();
    }

    public void SearchContainerPaths()
    {
        var bundleBase = mgr.GetBaseField(CABFile, assetBundle);

        string bundleName = Path.GetFileName(location.InternalId);
        string entryPath = String.Empty;

        foreach (var asset in bundleBase["m_Container.Array"])
        {
            string path = asset["first"].AsString;
            long pathId = asset["second.asset.m_PathID"].AsLong;

            var assetExt = mgr.GetExtAsset(CABFile, 0, pathId);

            if (CheckMissingAsset(path, pathId, out var assetGUID, out var newPath))
                continue; // assetGUID = CreateMissingAsset(assetPath, pathId);

            // First hit on an asset
            if (!pathCounts.TryGetValue(assetGUID, out int pathCount))
            {

                //Debug.Log($"Prepping asset representation for {path}");

                pathCounts[assetGUID] = 0;
                assetRepresentations[assetGUID] = ContentBuildInterface.GetPlayerAssetRepresentations(new GUID(assetGUID), EditorUserBuildSettings.activeBuildTarget);

            }

            if (IsFolderBundle)
            {
                entryPath = getCommonPath(entryPath, newPath);

                // Means the bundle addresses a folder but has only a single asset in it. I love reverse engineering stuff.
                if (bundleName.Split(".").Length == 2 && AssetCount == 1)
                    entryPath = getFolderPath(path, bundleName);

                // Debug.Log($"This is a folder bundle, common path of {path} {entryPath} ");

            } 
            else
            {
                //Debug.Log($"Creating label entry for {path} {assetGUID}");
                CreateEntry(assetGUID);
            }

            // Debug.Log($"Creating reference for object {assetExt.baseField["m_Name"].AsString} in {Path.GetFileName(location.InternalId)}");
            CreateReference(pathId, assetGUID, newPath);

        }

        if (IsFolderBundle)
        {
            var assetGUID = AssetDatabase.AssetPathToGUID(entryPath, AssetPathToGUIDOptions.OnlyExistingAssets);
            if (!assetGUID.Equals(string.Empty))
                CreateEntry(assetGUID);
        }

    }

    public void GenerateBundleName()
    {

        if (entries.Count == 0) return;

        string groupHash = CalculateGroupHash(schema.InternalBundleIdMode, assetGroup, entries);
        string address = string.Empty;

        switch (schema.BundleMode)
        {
            case BundledAssetGroupSchema.BundlePackingMode.PackTogether:
                address = $"all";
                break;
            case BundledAssetGroupSchema.BundlePackingMode.PackSeparately:
                address = entries[0].address;
                break;
            case BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel:
                var sb = new StringBuilder();
                foreach (var l in entries[0].labels)
                    sb.Append(l);
                address = sb.ToString();
                break;
        }

        string bundleName = $"{groupHash}_assets_{address}.bundle";
        bundleName = bundleName.ToLower().Replace(" ", "").Replace('\\', '/').Replace("//", "/");

        string hashedBundleName = HashingMethods.Calculate(bundleName) + ".bundle";
        referenceEntry.internalName = hashedBundleName;

    }


    public bool CheckMissingAsset(string assetPath, long pathId, out string assetGUID, out string newPath)
    {
        newPath = assetPath;
        assetGUID = AssetDatabase.AssetPathToGUID(assetPath, AssetPathToGUIDOptions.OnlyExistingAssets);

        // var assetDep = new AssetDependencies(mgr, CABFile);
        if (assetGUID.Equals(""))
        {

            var tempAssetGUID = SearchAssetMultipleFormat(assetPath, pathId);

            if (tempAssetGUID.Equals(""))
            {
                assetGUID = CreateMissingAsset(assetPath, pathId);
            } 
            else
            {
                assetGUID = tempAssetGUID;
                newPath = AssetDatabase.GUIDToAssetPath(assetGUID);
            }

        }

        return assetGUID.Equals("");

    }

    /// <summary>
    /// TODO Make it a post processing task because there might be multiple assets with the same container paths 
    /// but the asset doesn't exist yet and we don't want to create the wrong one
    /// </summary>
    /// <param name="assetPath"></param>
    /// <param name="pathId"></param>
    /// <returns>Asset GUID</returns>
    public string CreateMissingAsset(string assetPath, long pathId)
    {
        var assetExt = mgr.GetExtAsset(CABFile, 0, pathId);

        if (assetExt.baseField.TypeName == "SpriteAtlas")
        {

            Debug.Log($"Bundle: {location.PrimaryKey}");

            SpriteAtlas sa = new SpriteAtlas();
            AssetDatabase.CreateAsset(sa, assetPath);

            string atlasGUID = AssetDatabase.AssetPathToGUID(assetPath);
            
            foreach (var sprite in assetExt.baseField["m_PackedSpriteNamesToIndex.Array"])
            {

                string spriteName = sprite.AsString.Replace("]", "_");

                var sprites = AssetDatabase.FindAssets($"{spriteName} t:Sprite");
                string spriteGuid = sprites[0];
                
                if (sprites.Length > 1)
                {
                    // Debug.Log($"More than one sprite found for {spriteName}");

                    foreach (var matchedSprite in sprites)
                    {
                        if (Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(matchedSprite)).Equals(spriteName))
                        {
                            Debug.Log($"Asset is specifically {AssetDatabase.GUIDToAssetPath(matchedSprite)}");
                            spriteGuid = matchedSprite;
                            break;
                        }
                    }

                } 
                else if (sprites.Length == 0)
                {
                    Debug.Log($"No sprite found for {spriteName}");
                }

                string spritePath = AssetDatabase.GUIDToAssetPath(spriteGuid);
                var spriteObject = AssetDatabase.LoadAssetAtPath(spritePath, typeof(Sprite));

                sa.Add(new []{ spriteObject } );
                
            }

            if (sa.spriteCount != assetExt.baseField["m_PackedSpriteNamesToIndex.Array"].AsArray.size)
            {
                Debug.Log($"Sprite count issue for atlas {location.PrimaryKey}! in atlas: {assetExt.baseField["m_PackedSpriteNamesToIndex.Array"].AsArray.size} in assets {sa.spriteCount}");
            }

            return atlasGUID;

        }
        else
        {
            Debug.Log($"Bundle: {location.PrimaryKey}, Asset {assetPath} is missing for good! It needs to be created!");
        }

        return string.Empty;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="assetGUID"></param>
    /// <param name="assetPath"></param>
    public void CreateEntry(string assetGUID, string assetPath = null)
    {

        if (AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(assetGUID) != null) {
            var oldEntry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(assetGUID);
            if (oldEntry.parentGroup.SchemaTypes.Contains(typeof(AddressableReferenceSchema)))
                entries.Add(oldEntry);
                return;
        }

        var entry = AddressableAssetSettingsDefaultObject.Settings.CreateOrMoveEntry(
            assetGUID,
            assetGroup,
            false,
            true
        );

        if ( entry != null )
            entry.SetAddress(assetPath);

        if (schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel)
        {
            var label = Regex.Replace(location.PrimaryKey.Split("_assets_").Last().Replace(".bundle", ""), "_[0-9a-f]{32}", "");
            entry.SetLabel(label, true);
        }

        entries.Add(entry);

    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pathid"></param>
    /// <param name="assetGUID"></param>
    public void CreateReference(long pathId, string assetGUID, string assetPath)
    {

        var assetExt = mgr.GetExtAsset(CABFile, 0, pathId);
        
        ObjectIdentifier obid = new();

        // Debug.Log($"Asset {assetPath} - {assetExt.baseField["m_Name"].AsString} is a {assetExt.baseField.TypeName} {assetRepresentations[assetGUID].Length}");

        foreach (var oid in assetRepresentations[assetGUID])
        {
            var o = ObjectIdentifier.ToObject(oid);

            // Debug.Log($"Object: {o.name} {o.GetType()}");

            if (CheckAsset(o, assetExt))
            {
                obid = oid;
                // Debug.Log($"Ref: {assetPath} {o.name} {o.GetType()}");
            }
        }

        // Debug.Log($"Identifier for {assetPath} {assetGUID} {obid}");

        
        referenceEntry.cabName = CABFile.name;
        referenceEntry.baseInternalId = location.InternalId.Replace(UnityEngine.AddressableAssets.Addressables.RuntimePath, "{UnityEngine.AddressableAssets.Addressables.RuntimePath}");

        if (obid.localIdentifierInFile != 0)
            referenceEntry.m_ObjectMapping.Add(new ObjectMapping(obid, pathId));

    }


    // Utilities 
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
    
    public bool AlreadySeenPath(string assetPath)
    {
        return alreadySeenPaths.Contains(assetPath);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="assetPath"></param>
    /// <param name="pathId"></param>
    /// <returns></returns>
    private string SearchAssetMultipleFormat(string assetPath, long pathId)
    {

        var assetGUID = string.Empty;

        var assetExt = mgr.GetExtAsset(CABFile, 0, pathId);

        var internalAssetName = assetExt.baseField["m_Name"].AsString;
        var extension = assetPath.Split(".").Last();
        var basePath = assetPath.Replace($".{extension}", "");
        var folderPath = Path.GetDirectoryName(assetPath);

        // GG to the person who managed to put an extra space in the container path or asset name
        assetGUID = AssetDatabase.AssetPathToGUID($"{basePath.Trim()}.{extension}", AssetPathToGUIDOptions.OnlyExistingAssets);
        if (!assetGUID.Equals(""))
            return assetGUID;

        // Sometimes there are comma in the container path?
        assetGUID = AssetDatabase.AssetPathToGUID($"{assetPath.Replace(",", "_")}", AssetPathToGUIDOptions.OnlyExistingAssets);
        if (!assetGUID.Equals(""))
            return assetGUID;

        // In case someone makes a clone and forget it is one
        assetGUID = AssetDatabase.AssetPathToGUID($"{assetPath.Replace("(Clone)", "")}", AssetPathToGUIDOptions.OnlyExistingAssets);
        if (!assetGUID.Equals(""))
            return assetGUID;


        // Alternate path using the Asset name in the bundle instead of the container path
        assetGUID = AssetDatabase.AssetPathToGUID($"{folderPath}/{internalAssetName}.{extension}", AssetPathToGUIDOptions.OnlyExistingAssets);
        if (!assetGUID.Equals(""))
            return assetGUID;


        var formats = FileFormatList.GetFormatList(assetExt.baseField.TypeName);
        if (formats != null && assetGUID.Equals(""))
        {
            foreach (var format in formats)
            {

                assetGUID = AssetDatabase.AssetPathToGUID($"{basePath}{format}", AssetPathToGUIDOptions.OnlyExistingAssets);

                if (!assetGUID.Equals(""))
                    return assetGUID;
            }
        }

        return assetGUID;
    }

    private bool CheckAsset(UnityEngine.Object obj, AssetExternal assetExt)
    {

        string actualType = assetExt.baseField.TypeName;

        if (actualType == "MonoBehaviour") {
            actualType = mgr.GetExtAsset(monoscriptFile, 0, assetExt.baseField["m_Script.m_PathID"].AsLong).baseField["m_ClassName"].AsString;
            // Debug.Log($"Found MonoBehaviour for {assetExt.baseField["m_Name"].AsString}, actual type is {actualType}");
        }

        if (actualType == "Shader")
        {
            return true;
            // Debug.LogWarning($"{assetExt.baseField["m_ParsedForm.m_Name"].AsString} Identified as shader check for possible subassets");
        }

        return (obj.GetType().Name == actualType && obj.name == assetExt.baseField["m_Name"].AsString);
        
    }

    internal static string CalculateGroupHash(BundledAssetGroupSchema.BundleInternalIdMode mode, AddressableAssetGroup assetGroup, IEnumerable<AddressableAssetEntry> entries)
    {
        switch (mode)
        {
            case BundledAssetGroupSchema.BundleInternalIdMode.GroupGuid:
                return assetGroup.Guid;
            case BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdHash:
                return HashingMethods.Calculate(assetGroup.Guid, Application.cloudProjectId).ToString();
            case BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdEntriesHash:
                return HashingMethods.Calculate(assetGroup.Guid, Application.cloudProjectId, new HashSet<string>(entries.Select(e => e.guid))).ToString();
        }

        throw new Exception("Invalid naming mode.");
    }

}

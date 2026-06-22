using AddressableReferencer.Editor.Analyzer;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.U2D;
using static UnityEditor.FilePathAttribute;

public class BundleAnalyzer
{

    string StreamingAssetsPath;

    public IResourceLocation location;
    public AddressableAssetGroup assetGroup;
    public BundledAssetGroupSchema schema;
    public AddressableReferenceSchema referenceSchema;
    public AddressableReferenceEntry referenceEntry;

    public RentedFileArray rfa;
    public AssetsManager mgr;
    public BundleFileInstance bundle;
    public AssetsFileInstance CABFile;
    public AssetFileInfo assetBundle;

    // Monoscript bundle lookup to get the MonoBehaviour underlying types
    IResourceLocation monoscriptLocation;
    BundleFileInstance monoscriptBundle;
    public AssetsFileInstance monoscriptFile;

    List<AddressableAssetEntry> entries = new();

    private bool IsLoaded { get; set; } = false;

    private bool IsFolderBundle { 
        get {

            if (!IsLoaded)
                return false;

             return schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
        }
    }

    // Temp
    public string[] Labels
    {
        get
        {
            if (schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel)
            {
                return new string[] { Regex.Replace(location.PrimaryKey.Split("_assets_").Last().Replace(".bundle", ""), "_[0-9a-f]{32}", "") };
            }

            return null;
        }
    }

    public int AssetCount
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
        mgr = new()
        {
            UseQuickLookup = true,
            UseMonoTemplateFieldCache = true,
            UseRefTypeManagerCache = true,
            UseTemplateFieldCache = true,
        };

        location = loc;
        assetGroup = grp;
        StreamingAssetsPath = streamingAssetPath;

        schema = (BundledAssetGroupSchema)assetGroup.Schemas.Find(s => s is BundledAssetGroupSchema);
        referenceSchema = (AddressableReferenceSchema)assetGroup.Schemas.Find(s => s is AddressableReferenceSchema);

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

        rfa = new RentedFileArray(ResolveBundlePath(location));

        bundle = mgr.LoadBundleFile(rfa.Stream, ResolveBundlePath(location));
        CABFile = mgr.LoadAssetsFileFromBundle(bundle, 0, false);

        assetBundle = CABFile.file.GetAssetsOfType(AssetClassID.AssetBundle).First();

        var bundleBase = mgr.GetBaseField(CABFile, assetBundle);

        referenceEntry = referenceSchema.Entries.Find(e => e.cabName.Equals(CABFile.name));

        if (referenceEntry == null)
        {
            referenceEntry = new();
            referenceSchema.Entries.Add(referenceEntry);
        }

        referenceEntry.cabName = CABFile.name;
        referenceEntry.baseInternalId = location.InternalId.Replace(UnityEngine.AddressableAssets.Addressables.RuntimePath, "{UnityEngine.AddressableAssets.Addressables.RuntimePath}");

    }

    public void LoadMonoscript()
    {
        monoscriptBundle = mgr.LoadBundleFile(ResolveBundlePath(monoscriptLocation));
        monoscriptFile = mgr.LoadAssetsFileFromBundle(monoscriptBundle, 0, false);
    }


    // Actual processing
    public void ProcessBundle()
    {

        if (referenceEntry.isDone) { 
            rfa.Dispose();
            return;
        }

        var bundleBase = mgr.GetBaseField(CABFile, assetBundle);
        bool isScene = bundleBase["m_IsStreamedSceneAssetBundle"].AsBool;

        if (isScene)
        {
            return;
        }

        AnalyzeAssets();
        GenerateBundleName();

        referenceEntry.isDone = true;

        mgr.UnloadAll();
        GC.Collect();

        rfa.Dispose();

    }

    public void AnalyzeAssets()
    {
        using (var progressTracker = new UnityEditor.Build.Pipeline.Utilities.ProgressTracker()) { 
            var bundleBase = mgr.GetBaseField(CABFile, assetBundle);

            string bundleName = Path.GetFileName(location.InternalId);
            string entryPath = String.Empty;

            int counter = 0;

            if (IsFolderBundle)
            {
                var analyzer = new FolderAnalyzer(this);

                var kvp = analyzer.Analyze(0, "");

                if (kvp.Item1 != null)
                    entries.Add(kvp.Item1);

                if (kvp.Item2 != null)
                    referenceEntry.m_ObjectMapping.AddRange(kvp.Item2);

            } else { 

                foreach (var asset in bundleBase["m_Container.Array"])
                {
                    string path = asset["first"].AsString;
                    long pathId = asset["second.asset.m_PathID"].AsLong;
                    var assetExt = mgr.GetExtAsset(CABFile, 0, pathId);
                    
                    progressTracker.UpdateInfo($"({++counter}/{AssetCount}) - Processing: {Path.GetFileName(path)}");

                    var analyzer = GenericAnalyzer.GetAnalyzer(assetExt.baseField.TypeName, this);

                    var kvp = analyzer.Analyze(pathId, path);

                    if (kvp.Item1 != null)
                        entries.Add(kvp.Item1);

                    if (kvp.Item2 != null) {
                        referenceEntry.m_ObjectMapping.AddRange(kvp.Item2);
                    
                    }
                }
            }
        }
    }

    //public void SearchContainerPaths()
    //{
    //    var bundleBase = mgr.GetBaseField(CABFile, assetBundle);

    //    string bundleName = Path.GetFileName(location.InternalId);
    //    string entryPath = String.Empty;

    //    foreach (var asset in bundleBase["m_Container.Array"])
    //    {
    //        string path = asset["first"].AsString;
    //        long pathId = asset["second.asset.m_PathID"].AsLong;

    //        var assetExt = mgr.GetExtAsset(CABFile, 0, pathId);

    //        if (CheckMissingAsset(path, pathId, out var assetGUID, out var newPath))
    //            continue; // assetGUID = CreateMissingAsset(assetPath, pathId);

    //        if (IsFolderBundle)
    //        {
    //            entryPath = getCommonPath(entryPath, newPath);

    //            // Means the bundle addresses a folder but has only a single asset in it. I love reverse engineering stuff.
    //            if (bundleName.Split(".").Length == 2 && AssetCount == 1)
    //                entryPath = getFolderPath(path, bundleName);                
    //        } 
    //        else
    //        {
    //            CreateEntry(assetGUID);
    //        }

    //        CreateReference(pathId, assetGUID, newPath);

    //    }

    //    if (IsFolderBundle)
    //    {
    //        var assetGUID = AssetDatabase.AssetPathToGUID(entryPath, AssetPathToGUIDOptions.OnlyExistingAssets);
    //        if (!assetGUID.Equals(string.Empty))
    //            CreateEntry(assetGUID);
    //    }

    //}

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

    //public bool CheckMissingAsset(string assetPath, long pathId, out string assetGUID, out string newPath)
    //{
    //    newPath = assetPath;
    //    assetGUID = AssetDatabase.AssetPathToGUID(assetPath, AssetPathToGUIDOptions.OnlyExistingAssets);

    //    // var assetDep = new AssetDependencies(mgr, CABFile);
    //    if (assetGUID.Equals(""))
    //    {

    //        var tempAssetGUID = SearchAssetMultipleFormat(assetPath, pathId);

    //        if (tempAssetGUID.Equals(""))
    //        {
    //            assetGUID = CreateMissingAsset(assetPath, pathId);
    //        } 
    //        else
    //        {
    //            assetGUID = tempAssetGUID;
    //            newPath = AssetDatabase.GUIDToAssetPath(assetGUID);
    //        }

    //    }

    //    return assetGUID.Equals("");

    //}

    ///// <summary>
    ///// TODO Make it a post processing task because there might be multiple assets with the same container paths 
    ///// but the asset doesn't exist yet and we don't want to create the wrong one
    ///// </summary>
    ///// <param name="assetPath"></param>
    ///// <param name="pathId"></param>
    ///// <returns>Asset GUID</returns>
    //public string CreateMissingAsset(string assetPath, long pathId)
    //{
    //    var assetExt = mgr.GetExtAsset(CABFile, 0, pathId);

    //    if (assetExt.baseField.TypeName == "SpriteAtlas")
    //    {

    //        Debug.Log($"Bundle: {location.PrimaryKey}");

    //        SpriteAtlas sa = new SpriteAtlas();
    //        AssetDatabase.CreateAsset(sa, assetPath);

    //        string atlasGUID = AssetDatabase.AssetPathToGUID(assetPath);
            
    //        foreach (var sprite in assetExt.baseField["m_PackedSpriteNameToIndex.Array"])
    //        {

    //            string spriteName = sprite.AsString.Replace("]", "_");

    //            var sprites = AssetDatabase.FindAssets($"{spriteName} t:Sprite");
    //            string spriteGuid = sprites[0];
                
    //            if (sprites.Length > 1)
    //            {
    //                // Debug.Log($"More than one sprite found for {spriteName}");

    //                foreach (var matchedSprite in sprites)
    //                {
    //                    if (Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(matchedSprite)).Equals(spriteName))
    //                    {
    //                        Debug.Log($"Asset is specifically {AssetDatabase.GUIDToAssetPath(matchedSprite)}");
    //                        spriteGuid = matchedSprite;
    //                        break;
    //                    }
    //                }

    //            } 
    //            else if (sprites.Length == 0)
    //            {
    //                Debug.Log($"No sprite found for {spriteName}");
    //            }


    //            string spritePath = AssetDatabase.GUIDToAssetPath(spriteGuid);
    //            var spriteObject = AssetDatabase.LoadAssetAtPath(spritePath, typeof(Sprite));

    //            sa.Add(new []{ spriteObject } );
                
    //        }

    //        if (sa.spriteCount != assetExt.baseField["m_PackedSpriteNamesToIndex.Array"].AsArray.size)
    //        {
    //            Debug.Log($"Sprite count issue for atlas {location.PrimaryKey}! in atlas: {assetExt.baseField["m_PackedSpriteNamesToIndex.Array"].AsArray.size} in assets {sa.spriteCount}");
    //        }

    //        return atlasGUID;

    //    }
    //    else
    //    {
    //        Debug.Log($"Bundle: {location.PrimaryKey}, Asset {assetPath} is missing for good! It needs to be created!");
    //    }

    //    return string.Empty;
    //}

    ////public void CreateEntry(string assetGUID, string assetPath = null)
    ////{

    ////    if (AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(assetGUID) != null) {
    ////        var oldEntry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(assetGUID);
    ////        if (oldEntry.parentGroup.SchemaTypes.Contains(typeof(AddressableReferenceSchema)))
    ////            entries.Add(oldEntry);
    ////            return;
    ////    }

    ////    var entry = AddressableAssetSettingsDefaultObject.Settings.CreateOrMoveEntry(
    ////        assetGUID,
    ////        assetGroup,
    ////        false,
    ////        true
    ////    );

    ////    if ( entry != null )
    ////        entry.SetAddress(assetPath);

    ////    if (schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel)
    ////    {
    ////        var label = Regex.Replace(location.PrimaryKey.Split("_assets_").Last().Replace(".bundle", ""), "_[0-9a-f]{32}", "");
    ////        entry.SetLabel(label, true);
    ////    }

    ////    entries.Add(entry);

    ////}

    ////public void CreateReference(long pathId, string assetGUID, string assetPath)
    ////{

    ////    var assetExt = mgr.GetExtAsset(CABFile, 0, pathId);
        
    ////    ObjectIdentifier obid = new();

    ////    // Debug.Log($"Asset {assetPath} - {assetExt.baseField["m_Name"].AsString} is a {assetExt.baseField.TypeName} {assetRepresentations[assetGUID].Length}");

    ////    foreach (var oid in assetRepresentations[assetGUID])
    ////    {
    ////        var o = ObjectIdentifier.ToObject(oid);

    ////        // Debug.Log($"Object: {o.name} {o.GetType()}");

    ////        if (CheckAsset(o, assetExt))
    ////        {
    ////            obid = oid;
    ////            // Debug.Log($"Ref: {assetPath} {o.name} {o.GetType()}");
    ////        }

    ////    }

    ////    if (AssetDatabase.GetMainAssetTypeFromGUID(new GUID(assetGUID)).Name.Equals("SpriteAtlas"))
    ////    {
    ////        CreateSpriteAtlasReferences(pathId);
    ////    }

    ////    // Debug.Log($"Identifier for {assetPath} {assetGUID} {obid}");

    ////    if (obid.localIdentifierInFile != 0)
    ////        referenceEntry.m_ObjectMapping.Add(new ObjectMapping(obid, pathId));

    ////}

    //public void CreateSpriteAtlasReferences(long atlasId)
    //{
    //    var assetExt = mgr.GetExtAsset(CABFile, 0, atlasId);

    //    foreach (var sprite in assetExt.baseField["m_PackedSprites.Array"])
    //    {
    //        var spritePathId = sprite["m_PathID"].AsLong;

    //        if (sprite["m_FileID"].AsInt != 0)
    //            continue;

    //        var spriteAsset = mgr.GetExtAsset(CABFile, 0, spritePathId);

    //        Debug.Log($"Atlas: {assetExt.baseField["m_Name"].AsString} {spriteAsset.baseField == null}");

    //        string spriteName = spriteAsset.baseField["m_Name"].AsString.Replace("]", "_");

    //        var sprites = AssetDatabase.FindAssets($"{spriteName} t:Sprite");
    //        string spriteGuid = sprites[0];

    //        if (sprites.Length > 1)
    //        {

    //            foreach (var matchedSprite in sprites)
    //            {
    //                if (Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(matchedSprite)).Equals(spriteName))
    //                {
    //                    Debug.Log($"Asset is specifically {AssetDatabase.GUIDToAssetPath(matchedSprite)}");
    //                    spriteGuid = matchedSprite;
    //                    break;
    //                }
    //            }

    //        }
    //        else if (sprites.Length == 0)
    //        {
    //            Debug.Log($"No sprite found for {spriteName}");
    //        }


    //        string spritePath = AssetDatabase.GUIDToAssetPath(spriteGuid);
    //        Sprite spriteObject = AssetDatabase.LoadAssetAtPath(spritePath, typeof(Sprite)) as Sprite;

    //        ObjectIdentifier.TryGetObjectIdentifier(spriteObject, out ObjectIdentifier objectId);
    //        referenceEntry.m_ObjectMapping.Add(new ObjectMapping(objectId, spritePathId));

    //    }

    //}



    // Utilities 
    //private string getFolderPath(string path, string bundleName)
    //{

    //    while (!Path.GetFileNameWithoutExtension(path).ToLower().Replace(" ", "").Equals(Path.GetFileNameWithoutExtension(bundleName)))
    //    {
    //        path = Path.GetDirectoryName(path);
    //    }

    //    return path;

    //}

    //private string getCommonPath(string first, string second)
    //{

    //    if (first.Equals(string.Empty))
    //        return second;

    //    if (second.Equals(string.Empty))
    //        return string.Empty;


    //    var lf = first.Split("/");
    //    var ls = second.Split("/");

    //    int minL = Math.Min(lf.Length, ls.Length);

    //    string commonPath = "Assets";

    //    for (int i = 1; i < minL; i++)
    //    {
    //        if (lf[i].Equals(ls[i]))
    //        {
    //            commonPath = $"{commonPath}/{lf[i]}";
    //        }
    //    }

    //    return commonPath.Trim('/');


    //}
    
    //public bool AlreadySeenPath(string assetPath)
    //{
    //    return alreadySeenPaths.Contains(assetPath);
    //}

    //private string SearchAssetMultipleFormat(string assetPath, long pathId)
    //{

    //    var assetGUID = string.Empty;

    //    var assetExt = mgr.GetExtAsset(CABFile, 0, pathId);

    //    var internalAssetName = assetExt.baseField["m_Name"].AsString;
    //    var extension = assetPath.Split(".").Last();
    //    var basePath = assetPath.Replace($".{extension}", "");
    //    var folderPath = Path.GetDirectoryName(assetPath);

    //    // GG to the person who managed to put an extra space in the container path or asset name
    //    assetGUID = AssetDatabase.AssetPathToGUID($"{basePath.Trim()}.{extension}", AssetPathToGUIDOptions.OnlyExistingAssets);
    //    if (!assetGUID.Equals(""))
    //        return assetGUID;

    //    // Sometimes there are comma in the container path?
    //    assetGUID = AssetDatabase.AssetPathToGUID($"{assetPath.Replace(",", "_")}", AssetPathToGUIDOptions.OnlyExistingAssets);
    //    if (!assetGUID.Equals(""))
    //        return assetGUID;

    //    // In case someone makes a clone and forget it is one
    //    assetGUID = AssetDatabase.AssetPathToGUID($"{assetPath.Replace("(Clone)", "")}", AssetPathToGUIDOptions.OnlyExistingAssets);
    //    if (!assetGUID.Equals(""))
    //        return assetGUID;


    //    // Alternate path using the Asset name in the bundle instead of the container path
    //    assetGUID = AssetDatabase.AssetPathToGUID($"{folderPath}/{internalAssetName}.{extension}", AssetPathToGUIDOptions.OnlyExistingAssets);
    //    if (!assetGUID.Equals(""))
    //        return assetGUID;


    //    var formats = FileFormatList.GetFormatList(assetExt.baseField.TypeName);
    //    if (formats != null && assetGUID.Equals(""))
    //    {
    //        foreach (var format in formats)
    //        {

    //            assetGUID = AssetDatabase.AssetPathToGUID($"{basePath}{format}", AssetPathToGUIDOptions.OnlyExistingAssets);

    //            if (!assetGUID.Equals(""))
    //                return assetGUID;
    //        }
    //    }

    //    return assetGUID;
    //}

    //private bool CheckAsset(UnityEngine.Object obj, AssetExternal assetExt)
    //{

    //    string actualType = assetExt.baseField.TypeName;

    //    if (actualType == "MonoBehaviour") {
    //        actualType = mgr.GetExtAsset(monoscriptFile, 0, assetExt.baseField["m_Script.m_PathID"].AsLong).baseField["m_ClassName"].AsString;
    //        // Debug.Log($"Found MonoBehaviour for {assetExt.baseField["m_Name"].AsString}, actual type is {actualType}");
    //    }

    //    if (actualType == "Shader")
    //    {
    //        return true;
    //        // Debug.LogWarning($"{assetExt.baseField["m_ParsedForm.m_Name"].AsString} Identified as shader check for possible subassets");
    //    }

    //    return (obj.GetType().Name == actualType && obj.name == assetExt.baseField["m_Name"].AsString);
        
    //}

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

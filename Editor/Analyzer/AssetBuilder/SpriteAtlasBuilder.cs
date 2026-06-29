using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.Text;
using UnityEngine.U2D;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;

public class SpriteAtlasBuilder : GenericBuilderT<SpriteAtlas>
{
    public SpriteAtlasBuilder(BundleAnalyzer parentAnalyzer) : base(parentAnalyzer)
    {
    }
    
    private record TextureNameInfo(string index, string resolution, string format, string name, string hash);
    private TextureNameInfo ExtractTextureNameInfo(string textureName)
    {
        // sactx-[Index]-[Resolution]-[Format]-[AtlasName]-[Hash]

        if (!textureName.StartsWith("sactx"))
        {
            // Debug.LogError($"ExtractTextureNameInfo : {textureName} is an invalid texture name");
            return null;
        }

        string[] textureNameParts = textureName.Split("-");

        if (textureNameParts.Length != 6)
        {
            // Debug.LogError($"ExtractTextureNameInfo : {textureName} is an invalid texture name");
            return null;
        }

        return new TextureNameInfo(textureNameParts[1], textureNameParts[2], textureNameParts[3].Replace("|", "_"), textureNameParts[4], textureNameParts[5]);

    }
    private bool isValidTextureName(string textureName)
    {
        return ExtractTextureNameInfo(textureName) != null;
    }

    public override string CreateMissingAsset(long pathId, string assetPath)
    {

        var atlasBundleAsset = AssetManager.GetExtAsset(CabFile, 0, pathId);

        SpriteAtlas sa = new SpriteAtlas();

        if (!CheckAndCreateMissingFolder(assetPath)) {
            Debug.LogError($"Folder missing and impossible to create for {assetPath}");
            return string.Empty;
        }

        AssetDatabase.CreateAsset(sa, assetPath);
        string atlasGUID = AssetDatabase.AssetPathToGUID(assetPath);

        for (int i = 0; i < atlasBundleAsset.baseField["m_PackedSprites.Array"].AsArray.size; i++)
        {

            string spriteGuid = string.Empty;

            if (atlasBundleAsset.baseField["m_PackedSprites.Array"][i]["m_FileID"].AsInt == 0)
            {
                spriteGuid = SearchInternalSprite(atlasBundleAsset, i);
            } 
            else if (atlasBundleAsset.baseField["m_PackedSprites.Array"][i]["m_FileID"].AsInt > 0)
            {
                spriteGuid = SearchExternalSprite(atlasBundleAsset, i);
            }
            else
            {
                Debug.LogError($"Found a negative file Id {atlasBundleAsset.baseField["m_PackedSprites.Array"][i]["m_FileID"].AsInt}");
            }

            if (!spriteGuid.Equals(string.Empty)) { 
                string spritePath = AssetDatabase.GUIDToAssetPath(spriteGuid);
                var spriteObject = AssetDatabase.LoadAssetAtPath(spritePath, typeof(Sprite));

                sa.Add(new[] { spriteObject });
            }

        }

        return atlasGUID;
    }

    public string SearchInternalSprite(AssetExternal atlasBundleAsset, int index)
    {
        string spriteName = string.Empty;

        var spriteAsset = AssetManager.GetExtAsset(CabFile, 0, atlasBundleAsset.baseField["m_PackedSprites.Array"][index]["m_PathID"].AsLong);
        spriteName = spriteAsset.baseField["m_Name"].AsString;

        var sprites = AssetDatabase.FindAssets($"{spriteName.Replace("]", "")} t:Sprite");
        string spriteGuid = sprites[0];

        if (sprites.Length > 1)
        {

            // Debug.Log($"Found {sprites.Length} sprites for {spriteName}");

            List<string> finalSprites = new();

            var renderDataTexture = AssetManager.GetExtAsset(
                CabFile,
                0,
                atlasBundleAsset.baseField["m_RenderDataMap.Array"][index]["second.texture.m_PathID"].AsLong
            );

            var spriteRect = new Rect(
                atlasBundleAsset.baseField["m_RenderDataMap.Array"][index]["second.textureRect.x"].AsFloat,
                atlasBundleAsset.baseField["m_RenderDataMap.Array"][index]["second.textureRect.y"].AsFloat,
                atlasBundleAsset.baseField["m_RenderDataMap.Array"][index]["second.textureRect.width"].AsFloat,
                atlasBundleAsset.baseField["m_RenderDataMap.Array"][index]["second.textureRect.height"].AsFloat
            );

            foreach (var matchedSprite in sprites)
            {

                string tempSpritePath = AssetDatabase.GUIDToAssetPath(matchedSprite);
                Sprite tempSpriteObject = AssetDatabase.LoadAssetAtPath(tempSpritePath, typeof(Sprite)) as Sprite;

                if (tempSpriteObject.texture == null)
                    continue;

                if (isValidTextureName(tempSpriteObject.texture.name)) { 
                    if (!CompareTextureNames(renderDataTexture.baseField["m_Name"].AsString, tempSpriteObject.texture.name))
                        continue;
                } 
                else // In case the texture isn't an atlas, it happens with directly addressable paths
                {
                    if (!renderDataTexture.baseField["m_Name"].AsString.Replace("|", "_").Equals(tempSpriteObject.texture.name))
                        continue;
                }

                if (!tempSpriteObject.name.Equals(spriteName))
                    continue;

                if (!ComparePhysicsShapes(tempSpriteObject, spriteAsset))
                    continue;

                if (!tempSpriteObject.rect.Equals(spriteRect))
                    continue;

                finalSprites.Add(matchedSprite);

            }

            if (finalSprites.Count != 0)
            {
                if (finalSprites.Count > 1)
                {
                    Debug.LogWarning($"More than one sprite found for {spriteName} in Atlas {Path.GetFileNameWithoutExtension(Location.InternalId)} after filtering find a way to differentiate the following {finalSprites.Count} sprites {string.Join(", ", finalSprites.Select(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g))))}");
                }

                spriteGuid = finalSprites[0];
            }
            else
            {
                Debug.LogError($"No sprite matched for {spriteName} in Atlas {Path.GetFileNameWithoutExtension(Location.InternalId)} ");
            }

        }
        else if (sprites.Length == 0)
        {
            Debug.LogWarning($"No sprite found for {spriteName} in Atlas {Path.GetFileNameWithoutExtension(Location.InternalId)} ");
            return string.Empty;
        }

        return spriteGuid;

    }

    public string SearchExternalSprite(AssetExternal atlasBundleAsset, int index)
    {
        string spriteName = string.Empty;

        // var spriteAsset = AssetManager.GetExtAsset(CabFile, 0, atlasBundleAsset.baseField["m_PackedSpriteNamesToIndex.Array"][index].AsString);
        spriteName = atlasBundleAsset.baseField["m_PackedSpriteNamesToIndex.Array"][index].AsString;

        var sprites = AssetDatabase.FindAssets($"{spriteName.Replace("]", "")} t:Sprite");
        string spriteGuid = sprites[0];

        if (sprites.Length > 1)
        {

            // Debug.Log($"Found more than one sprite for {spriteName}");

            List<string> finalSprites = new();

            var renderDataTexture = AssetManager.GetExtAsset(
                CabFile,
                0,
                atlasBundleAsset.baseField["m_RenderDataMap.Array"][index]["second.texture.m_PathID"].AsLong
            );

            var spriteRect = new Rect(
                atlasBundleAsset.baseField["m_RenderDataMap.Array"][index]["second.textureRect.x"].AsFloat,
                atlasBundleAsset.baseField["m_RenderDataMap.Array"][index]["second.textureRect.y"].AsFloat,
                atlasBundleAsset.baseField["m_RenderDataMap.Array"][index]["second.textureRect.width"].AsFloat,
                atlasBundleAsset.baseField["m_RenderDataMap.Array"][index]["second.textureRect.height"].AsFloat
            );

            foreach (var matchedSprite in sprites)
            {
                string tempSpritePath = AssetDatabase.GUIDToAssetPath(matchedSprite);
                Sprite tempSpriteObject = AssetDatabase.LoadAssetAtPath(tempSpritePath, typeof(Sprite)) as Sprite;

                // Info not in the atlas bundle for external sprites
                //if (!CompareTextureNames(renderDataTexture.baseField["m_Name"].AsString, tempSpriteObject.texture.name))
                //    continue;

                if (!tempSpriteObject.name.Equals(spriteName))
                    continue;

                // Info not in the atlas bundle for external sprites
                //if (!ComparePhysicsShapes(tempSpriteObject, spriteAsset))
                //    continue;

                if (!tempSpriteObject.rect.Equals(spriteRect))
                    continue;

                finalSprites.Add(matchedSprite);

            }

            if (finalSprites.Count != 0)
            {
                if (finalSprites.Count > 1)
                {
                    Debug.LogWarning($"More than one sprite found for {spriteName} after filtering find a way to differentiate the following {finalSprites.Count} sprites {string.Join(", ", finalSprites.Select(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g))))}");
                }

                spriteGuid = finalSprites[0];
            }
            else
            {
                Debug.LogError($"No sprite matched for {spriteName}");
            }

        }
        else if (sprites.Length == 0)
        {
            Debug.LogWarning($"No sprite found for {spriteName}");
            return string.Empty;
        }

        return spriteGuid;

    }


    public bool ComparePhysicsShapes(Sprite sObject, AssetExternal spriteAsset)
    {

        var physicsShape = new List<Vector2>();

        var physicsArray = spriteAsset.baseField["m_PhysicsShape.Array"];
        var physicsArraySize = physicsArray.AsArray.size;

        if (physicsArraySize != sObject.GetPhysicsShapeCount())
            return false;

        for (int i = 0; i < physicsArraySize; i++)
        {

            sObject.GetPhysicsShape(i, physicsShape);

            var shapeArray = physicsArray[i][0];
            if (shapeArray.AsArray.size != physicsShape.Count)
                return false;

            for (int j = 0; j < shapeArray.AsArray.size; j++)
            {
                if (shapeArray[j]["x"].AsFloat != physicsShape[j].x || shapeArray[j]["y"].AsFloat != physicsShape[j].y)
                    return false;
            }

        }

        return true;

    }

    private bool CompareTextureNames(string t1, string t2)
    {

        TextureNameInfo et1 = ExtractTextureNameInfo(t1);
        TextureNameInfo et2 = ExtractTextureNameInfo(t2);

        if (et1 == null  || et2 == null) return false;

        if (!et1.name.Equals(et2.name)) return false;
        if (!et1.index.Equals(et2.index)) return false;

        // Should these two be considered? Or just log a warning?
        if (!et1.resolution.Equals(et2.resolution)) return false;

        if (!et1.format.Equals(et2.format)) return false;

        if (!et1.hash.Equals(et2.hash))
            Debug.LogWarning($"Sprite atlas textures have mismatching hashes. : {t1} {t2}");

        return true;
    }


}

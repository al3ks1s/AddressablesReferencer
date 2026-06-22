using AssetsTools.NET.Extra;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.U2D;
using static UnityEditor.AddressableAssets.Build.Layout.BuildLayout;
using static UnityEditor.FilePathAttribute;

public class SpriteAtlasBuilder : GenericBuilderT<SpriteAtlas>
{
    public SpriteAtlasBuilder(BundleAnalyzer parentAnalyzer) : base(parentAnalyzer)
    {
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
            string spriteName = string.Empty;

            if (atlasBundleAsset.baseField["m_PackedSprites.Array"][i]["m_FileID"].AsInt != 0)
            {
                Debug.LogWarning($"Sprite asset {atlasBundleAsset.baseField["m_PackedSpriteNamesToIndex.Array"][i].AsString} in atlas {Path.GetFileNameWithoutExtension(assetPath)} is external to the bundle, skippning but check if it still works");
                continue;
            }

            var spriteAsset = AssetManager.GetExtAsset(CabFile, 0, atlasBundleAsset.baseField["m_PackedSprites.Array"][i]["m_PathID"].AsLong);
            spriteName = spriteAsset.baseField["m_Name"].AsString.Replace("]", "");
   
            var sprites = AssetDatabase.FindAssets($"{spriteName} t:Sprite");
            string spriteGuid = sprites[0];

            if (sprites.Length > 1)
            {

                Debug.Log($"Found more than one sprite for {spriteName} in atlas {Path.GetFileNameWithoutExtension(assetPath)}");

                List<string> validTextureSprites = new();
                List<string> validPhysicsSprites = new();
                List<string> validRectSprites = new();

                var renderDataTexture = AssetManager.GetExtAsset(
                    CabFile,
                    0,
                    atlasBundleAsset.baseField["m_RenderDataMap.Array"][i]["second.texture.m_PathID"].AsLong
                );

                var spriteRect = new Rect(
                    atlasBundleAsset.baseField["m_RenderDataMap.Array"][i]["second.textureRect.x"].AsFloat,
                    atlasBundleAsset.baseField["m_RenderDataMap.Array"][i]["second.textureRect.y"].AsFloat,
                    atlasBundleAsset.baseField["m_RenderDataMap.Array"][i]["second.textureRect.width"].AsFloat,
                    atlasBundleAsset.baseField["m_RenderDataMap.Array"][i]["second.textureRect.height"].AsFloat                    
                );
               
                foreach (var matchedSprite in sprites)
                {

                    string tempSpritePath = AssetDatabase.GUIDToAssetPath(matchedSprite);
                    var tempSpriteObject = AssetDatabase.LoadAssetAtPath(tempSpritePath, typeof(Sprite)) as Sprite;

                    if (tempSpriteObject.texture == null)
                        continue;

                    if (renderDataTexture.baseField["m_Name"].AsString.Equals(tempSpriteObject.texture.name) && tempSpriteObject.name.Equals(spriteName))
                    {
                        validTextureSprites.Add(matchedSprite); 
                    }

                }

                if (validTextureSprites.Count > 1)
                {
                    foreach (var matchedSprite in validTextureSprites) { 
                        string tempSpritePath = AssetDatabase.GUIDToAssetPath(matchedSprite);
                        var tempSpriteObject = AssetDatabase.LoadAssetAtPath(tempSpritePath, typeof(Sprite)) as Sprite;
                        
                        if (ComparePhysicsShapes(tempSpriteObject, spriteAsset))
                            validPhysicsSprites.Add(matchedSprite);
                    }
                }

                if (validPhysicsSprites.Count > 1)
                {
                    foreach (var matchedSprite in validPhysicsSprites) { 
                        string tempSpritePath = AssetDatabase.GUIDToAssetPath(matchedSprite);
                        var tempSpriteObject = AssetDatabase.LoadAssetAtPath(tempSpritePath, typeof(Sprite)) as Sprite;

                        if (tempSpriteObject.rect.Equals(spriteRect))
                            validRectSprites.Add(matchedSprite);
                    }
                }

                if (validTextureSprites.Count == 1)
                    spriteGuid = validTextureSprites[0];

                if (validPhysicsSprites.Count == 1)
                    spriteGuid = validPhysicsSprites[0];

                if (validRectSprites.Count == 1)
                    spriteGuid = validRectSprites[0];

                Debug.Log($"After filtering, sprites for {spriteName} is {Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(spriteGuid))} in atlas {Path.GetFileNameWithoutExtension(assetPath)} {validTextureSprites.Count} {validPhysicsSprites.Count} {validRectSprites.Count}");

            }
            else if (sprites.Length == 0)
            {
                Debug.LogWarning($"No sprite found for {spriteName}");
                continue;
            }

            string spritePath = AssetDatabase.GUIDToAssetPath(spriteGuid);
            var spriteObject = AssetDatabase.LoadAssetAtPath(spritePath, typeof(Sprite));

            sa.Add(new[] { spriteObject });

        }
        return atlasGUID;
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
}

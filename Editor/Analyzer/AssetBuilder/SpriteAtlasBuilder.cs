using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

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

            string spriteGuid = string.Empty;

            if (atlasBundleAsset.baseField["m_PackedSprites.Array"][i]["m_FileID"].AsInt == 0)
            {
                spriteGuid = SearchInternalSprite(atlasBundleAsset, i);
            } 
            else if (atlasBundleAsset.baseField["m_PackedSprites.Array"][i]["m_FileID"].AsInt > 0)
            {
                Debug.LogWarning($"Sprite asset {atlasBundleAsset.baseField["m_PackedSpriteNamesToIndex.Array"][i].AsString} in atlas {Path.GetFileNameWithoutExtension(assetPath)} is external to the bundle, skippning but check if it still works");
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
        spriteName = spriteAsset.baseField["m_Name"].AsString.Replace("]", "");

        var sprites = AssetDatabase.FindAssets($"{spriteName} t:Sprite");
        string spriteGuid = sprites[0];

        if (sprites.Length > 1)
        {

            Debug.Log($"Found {sprites.Length} sprites for {spriteName}");

            List<string> validTextureSprites = new();
            List<string> validPhysicsSprites = new();
            List<string> validRectSprites = new();

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
                var tempSpriteObject = AssetDatabase.LoadAssetAtPath(tempSpritePath, typeof(Sprite)) as Sprite;

                if (tempSpriteObject.texture == null)
                {
                    Debug.LogError($"{tempSpriteObject.name} does not have a texture");
                    continue;
                }

                // Debug.Log($"{tempSpritePath}: {tempSpriteObject.texture.name} {renderDataTexture.baseField["m_Name"].AsString} {spriteName}");

                // TODO : Put a better name filter than just replacing pipes with underscores, try to match against textures with the same name when you remove character at index of pipe?
                if (renderDataTexture.baseField["m_Name"].AsString.Replace("|", "_").Equals(tempSpriteObject.texture.name) && tempSpriteObject.name.Equals(spriteName))
                {
                    validTextureSprites.Add(matchedSprite);
                }

            }

            if (validTextureSprites.Count > 1)
            {
                foreach (var matchedSprite in validTextureSprites)
                {
                    string tempSpritePath = AssetDatabase.GUIDToAssetPath(matchedSprite);
                    var tempSpriteObject = AssetDatabase.LoadAssetAtPath(tempSpritePath, typeof(Sprite)) as Sprite;

                    if (ComparePhysicsShapes(tempSpriteObject, spriteAsset))
                        validPhysicsSprites.Add(matchedSprite);
                }
            }

            if (validPhysicsSprites.Count > 1)
            {
                foreach (var matchedSprite in validPhysicsSprites)
                {
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

            Debug.Log($"After filtering, sprites for {spriteName} is {Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(spriteGuid))} {validTextureSprites.Count} {validPhysicsSprites.Count} {validRectSprites.Count}");

        }
        else if (sprites.Length == 0)
        {
            Debug.LogWarning($"No sprite found for {spriteName}");
            return string.Empty;
        }

        return spriteGuid;

    }

    public string SearchExternalSprite(AssetExternal atlasBundleAsset, int index)
    {
        string spriteName = string.Empty;

        // var spriteAsset = AssetManager.GetExtAsset(CabFile, 0, atlasBundleAsset.baseField["m_PackedSpriteNamesToIndex.Array"][index].AsString);
        spriteName = atlasBundleAsset.baseField["m_PackedSpriteNamesToIndex.Array"][index].AsString.Replace("]", "");

        var sprites = AssetDatabase.FindAssets($"{spriteName} t:Sprite");
        string spriteGuid = sprites[0];

        if (sprites.Length > 1)
        {

            Debug.Log($"Found more than one sprite for {spriteName}");

            List<string> validTextureSprites = new();
            // List<string> validPhysicsSprites = new();
            List<string> validRectSprites = new();

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
                var tempSpriteObject = AssetDatabase.LoadAssetAtPath(tempSpritePath, typeof(Sprite)) as Sprite;

                if (tempSpriteObject.texture == null)
                {
                    Debug.LogWarning($"{tempSpriteObject.name} does not have a texture, skipping this one but recheck");
                    continue;
                }

                Debug.Log($"{tempSpritePath}: {tempSpriteObject.texture.name} {renderDataTexture.baseField["m_Name"].AsString} {spriteName}");

                // TODO : Put a better name filter than just replacing pipes with underscores, try to match against textures with the same name when you remove character at index of pipe?
                if (tempSpriteObject.name.Equals(spriteName)) // renderDataTexture.baseField["m_Name"].AsString.Replace("|", "_").Equals(tempSpriteObject.texture.name)
                {
                    validTextureSprites.Add(matchedSprite);
                }

            }

            //if (validTextureSprites.Count > 1)
            //{
            //    foreach (var matchedSprite in validTextureSprites)
            //    {
            //        string tempSpritePath = AssetDatabase.GUIDToAssetPath(matchedSprite);
            //        var tempSpriteObject = AssetDatabase.LoadAssetAtPath(tempSpritePath, typeof(Sprite)) as Sprite;

            //        if (ComparePhysicsShapes(tempSpriteObject, spriteAsset))
            //            validPhysicsSprites.Add(matchedSprite);
            //    }
            //}

            if (validTextureSprites.Count > 1)
            {
                foreach (var matchedSprite in validTextureSprites)
                {
                    string tempSpritePath = AssetDatabase.GUIDToAssetPath(matchedSprite);
                    var tempSpriteObject = AssetDatabase.LoadAssetAtPath(tempSpritePath, typeof(Sprite)) as Sprite;

                    if (tempSpriteObject.rect.Equals(spriteRect))
                        validRectSprites.Add(matchedSprite);
                }
            }
            

            if (validTextureSprites.Count == 1)
                spriteGuid = validTextureSprites[0];

            //if (validPhysicsSprites.Count == 1)
            //    spriteGuid = validPhysicsSprites[0];

            if (validRectSprites.Count == 1)
                spriteGuid = validRectSprites[0];

            Debug.Log($"After filtering, sprites for {spriteName} is {Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(spriteGuid))} {validTextureSprites.Count} {validRectSprites.Count}");

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
}

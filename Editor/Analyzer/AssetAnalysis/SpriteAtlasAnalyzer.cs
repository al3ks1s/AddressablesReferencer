using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using static UnityEditor.AddressableAssets.Build.Layout.BuildLayout;

namespace AddressableReferencer.Editor.Analyzer
{
    public class SpriteAtlasAnalyzer : GenericAnalyzerT<SpriteAtlas>
    {

        public SpriteAtlasAnalyzer(BundleAnalyzer parentAnalyzer) : base(parentAnalyzer) { }

        public override (AddressableAssetEntry, List<ObjectMapping>) Analyze(long pathId, string assetPath)
        {
            // return (null, null);
            List<ObjectMapping> spriteObjects = new();

            // Create an entry and the mapping for the sprite atlas specifically, it will create the atlas if necessary
            var (entry, mapping) = base.Analyze(pathId, assetPath);

            if (mapping != null)
            {
                spriteObjects.AddRange(mapping);
            }

            var atlasBundleAsset = AssetManager.GetExtAsset(CabFile, 0, pathId);

            var sa = AssetDatabase.LoadAssetAtPath(entry.AssetPath, typeof(SpriteAtlas)) as SpriteAtlas;

            Debug.Log($"{sa.GetPackables().Length} sprites packed");

            var sprites = sa.GetPackables();

            int spriteIndex = 0;

            for (int i = 0; i < atlasBundleAsset.baseField["m_PackedSprites.Array"].AsArray.size; i++)
            {
                if (atlasBundleAsset.baseField["m_PackedSprites.Array"][i]["m_FileID"].AsInt != 0)
                    continue;

                var spritePathID = atlasBundleAsset.baseField["m_PackedSprites.Array"][i]["m_PathID"].AsLong;
                var spriteAsset = AssetManager.GetExtAsset(CabFile, 0, spritePathID);

                var packedSprite = sprites[spriteIndex] as Sprite;

                if (packedSprite.name != spriteAsset.baseField["m_Name"].AsString)
                    Debug.LogWarning($"Sprite name issue! {spriteAsset.baseField["m_Name"].AsString} is not {packedSprite.name}. Recheck if the object is the correct one");

                if (ObjectIdentifier.TryGetObjectIdentifier(packedSprite, out ObjectIdentifier objectId))
                    spriteObjects.Add(new ObjectMapping(objectId, spritePathID));

                spriteIndex++;

            }
            return (entry, spriteObjects);
        }

    }
}
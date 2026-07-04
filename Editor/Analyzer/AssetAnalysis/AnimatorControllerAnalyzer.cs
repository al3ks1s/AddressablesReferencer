using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Animations;
using UnityEditor.Build.Content;
using UnityEngine;

namespace AddressableReferencer.Editor.Analyzer
{
    public class AnimatorControllerAnalyzer : GenericAnalyzerT<AnimatorController>
    {
        public AnimatorControllerAnalyzer(BundleAnalyzer parentAnalyzer) : base(parentAnalyzer) { }

        public override (AddressableAssetEntry, List<ObjectMapping>) Analyze(long pathId, string assetPath)
        {

            var (entry, mapping) = base.Analyze(pathId, assetPath);

            if (entry == null)
                return (null, null);

            var clipSpriteReferences = GetClipReferences(entry.AssetPath);
            List<long> spriteReferences = GetAnimatorControllerSpritePathIds(pathId);

            foreach (var spriteReference in spriteReferences)
            {
                var sprite = AssetManager.GetExtAsset(CabFile, 0, spriteReference);
                if (clipSpriteReferences.TryGetValue(sprite.baseField["m_Name"].AsString, out var objectId))
                    mapping.Add(new ObjectMapping(objectId, spriteReference));
            }

            return (entry, mapping);
        }

        private Dictionary<string, ObjectIdentifier> GetClipReferences(string assetPath)
        {
            var clipRefs = new Dictionary<string, ObjectIdentifier>();
            var controllerObject = AssetDatabase.LoadMainAssetAtPath(assetPath) as AnimatorController;

            if (controllerObject == null)
                return null;

            foreach (var clip in controllerObject.animationClips)
            {
                var curvesRefBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);

                foreach (var binding in curvesRefBindings)
                {
                    var kframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    foreach (var kframe in kframes)
                    {
                        if (kframe.value == null) continue;

                        ObjectIdentifier.TryGetObjectIdentifier(kframe.value, out var objectIdentifier);
                        clipRefs.TryAdd(kframe.value.name, objectIdentifier);
                    }
                }
            }

            return clipRefs;
        }

        private List<long> GetAnimatorControllerSpritePathIds(long pathId)
        {
            List<long> spriteIds = new List<long>();
            var animatorController = AssetManager.GetExtAsset(CabFile, 0, pathId);

            foreach (var animationClip in animatorController.baseField["m_AnimationClips.Array"])
            {
                if (animationClip["m_FileID"].AsInt != 0 || animationClip["m_PathID"].AsLong == 0) continue;
                spriteIds.AddRange(GetAnimationClipSpritePathIds(animationClip["m_PathID"].AsLong));
            }

            return spriteIds;
        }

        private List<long> GetAnimationClipSpritePathIds(long pathId)
        {
            List<long> spriteIds = new List<long>();
            var animationClip = AssetManager.GetExtAsset(CabFile, 0, pathId);

            foreach (var curveMap in animationClip.baseField["m_ClipBindingConstant.pptrCurveMapping.Array"])
            {
                if (curveMap["m_FileID"].AsInt != 0 || curveMap["m_PathID"].AsLong == 0) continue;
                spriteIds.Add(curveMap["m_PathID"].AsLong);
            }

            return spriteIds;
        }
    }
}
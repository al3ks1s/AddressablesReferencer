using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AddressableReferencer.Editor.GUI { 

    public class AssetReplacer : EditorWindow
    {

        UnityEngine.Object assetToReplace;
        UnityEngine.Object assetToReplaceBy;

        public void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            assetToReplace = EditorGUILayout.ObjectField(assetToReplace, typeof(UnityEngine.Object), false);
            assetToReplaceBy = EditorGUILayout.ObjectField(assetToReplaceBy, typeof(UnityEngine.Object), false);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(new GUIContent("Replace all asset references")))
            {
                if (!CanBeReplaced())
                    return;

                Debug.Log($"Will replace {AssetDatabase.GetAssetPath(assetToReplace)} by {AssetDatabase.GetAssetPath(assetToReplaceBy)}");
            }
            
        }

        private bool CanBeReplaced()
        {
            if (assetToReplace == null || assetToReplaceBy == null)
            {
                EditorUtility.DisplayDialog("No selected assets", "One of the asset fields was left emtpy.", "Ok.");
                return false;
            }
            if (assetToReplace.GetType() != assetToReplaceBy.GetType())
            {
                EditorUtility.DisplayDialog("Asset Type Difference", "The selected assets are of different type.", "Ok.");
                return false;
            }
            return true;
        }

        private void ReplaceAssetsInScenes()
        {

            string originalScene = SceneManager.GetActiveScene().path;
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");

            int total = 0;

            AssetDatabase.StartAssetEditing();

            try
            {
                for (int i = 0; i < sceneGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                    int replaced = ReplaceAssetsInScene(scene);

                    if (replaced > 0)
                    {
                        EditorSceneManager.SaveScene(scene);
                        Debug.Log($"{path}: replaced {replaced}");
                    }

                    total += replaced;

                    EditorUtility.DisplayProgressBar(
                        "Replacing Sprites",
                        path,
                        (float)(i + 1) / sceneGuids.Length);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();

                if (!string.IsNullOrEmpty(originalScene))
                {
                    EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
                }
            }

            Debug.Log($"Finished. Replaced {total} sprite references.");
        
        }
        private int ReplaceAssetsInScene(Scene scene)
        {
            int replaced = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                //SpriteRenderer[] renderers =
                //    root.GetComponentsInChildren<SpriteRenderer>(includeInactive);

                //foreach (SpriteRenderer sr in renderers)
                //{
                //    if (sr.sprite != spriteToReplace)
                //        continue;

                //    Undo.RecordObject(sr, "Replace Sprite");

                //    sr.sprite = replacementSprite;

                //    EditorUtility.SetDirty(sr);

                //    replaced++;
                //}
            }

            EditorSceneManager.MarkSceneDirty(scene);

            return replaced;
        }
        private void ReplaceAssetsInNonSceneAssets()
        {

        }

    }
}
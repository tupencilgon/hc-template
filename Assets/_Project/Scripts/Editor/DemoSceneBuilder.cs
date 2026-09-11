using System.Collections.Generic;
using System.IO;
using HC.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HC.EditorTools
{
    /// <summary>
    /// Tạo scene demo để test các hệ thống Core bằng 1 cú click menu, khỏi phải dựng tay.
    /// Xoá file này cùng thư mục Demo khi bắt đầu game thật.
    /// </summary>
    public static class DemoSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Demo_Core.unity";

        [MenuItem("HC Template/Dựng scene demo Core", priority = 0)]
        public static void BuildDemoScene()
        {
            // Hỏi lưu scene đang mở trước, không thì người dùng mất việc đang làm dở.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var demo = new GameObject("CoreDemo");
            demo.AddComponent<CoreDemo>();

            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 4f, -12f);
                camera.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
                camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            AddToBuildSettings(ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[DemoSceneBuilder] Đã tạo {ScenePath} và thêm vào Build Settings. Bấm Play để test.");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
        }

        /// <summary>
        /// Thêm scene vào Build Settings — SceneLoader.ReloadScene() cần scene có trong danh sách này
        /// thì LoadSceneAsync mới load được.
        /// </summary>
        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != scenePath) continue;

                if (!scenes[i].enabled) scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

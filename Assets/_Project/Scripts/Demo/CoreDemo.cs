using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using HC.Core;

namespace HC.Demo
{
    /// <summary>
    /// Scene demo để kiểm chứng 4 hệ thống Core chạy thật: ObjectPool, AudioManager, SaveManager, SceneLoader.
    /// Toàn bộ UI, prefab và âm thanh đều sinh bằng code — không phụ thuộc asset nào.
    ///
    /// Đây là code TEST, không phải code game. Khi clone template cho game mới thì xoá
    /// nguyên thư mục Assets/_Project/Scripts/Demo và scene Demo_Core.
    /// </summary>
    public class CoreDemo : MonoBehaviour
    {
        private const string CubeKey = "demo_cube";

        private AudioClip _sfxClip;
        private AudioClip _bgmClip;

        private Text _statusText;
        private Text _muteButtonLabel;

        private int _spawnedThisSession;

        private void Start()
        {
            EnsureEventSystem();
            EnsureCamera();
            BuildUI();

            _sfxClip = CreateTone("sfx_blip", frequency: 880f, duration: 0.08f, volume: 0.5f);
            _bgmClip = CreateTone("bgm_hum", frequency: 220f, duration: 1f, volume: 0.2f);

            BuildGround();

            // Sau khi Reload scene, ObjectPool vẫn còn sống (DontDestroyOnLoad) và vẫn giữ pool cũ —
            // đăng ký lại bằng prefab mới sẽ bị từ chối. Hỏi HasPool trước cho sạch.
            if (!ObjectPool.Instance.HasPool(CubeKey))
            {
                var cubePrefab = CreateCubePrefab();
                DontDestroyOnLoad(cubePrefab); // prefab phải sống lâu hơn scene, không thì reload xong là hỏng pool
                ObjectPool.Instance.RegisterPool(CubeKey, cubePrefab, prewarmCount: 5, maxSize: 40);
            }

            AudioManager.Instance.PlayBGM(_bgmClip);

            RefreshStatus();
        }

        // ---------- Các nút demo ----------

        private void SpawnCube()
        {
            var cube = ObjectPool.Instance.Get(
                CubeKey,
                new Vector3(Random.Range(-4f, 4f), Random.Range(2f, 5f), Random.Range(-2f, 2f)),
                Random.rotation);

            if (cube == null) return;

            _spawnedThisSession++;
            AudioManager.Instance.PlaySFX(_sfxClip);

            // Trả về pool sau 2s — đây là chỗ thay cho Destroy().
            ObjectPool.Instance.Return(cube, 2f);
            RefreshStatus();
        }

        private void SpawnBurst()
        {
            // 20 cube trong 1 frame: test cả pool lẫn bộ chặn phát dồn SFX của AudioManager.
            for (int i = 0; i < 20; i++) SpawnCube();
        }

        private void ToggleMute()
        {
            AudioManager.Instance.ToggleMute();
            RefreshStatus();
        }

        private void AddCoins()
        {
            SaveManager.Data.coins += 10;
            SaveManager.Data.highScore = Mathf.Max(SaveManager.Data.highScore, _spawnedThisSession);
            SaveManager.Save();
            RefreshStatus();
        }

        private void DeleteSave()
        {
            SaveManager.Delete();
            RefreshStatus();
        }

        private void ReloadScene()
        {
            // Vào lại scene này: coins phải giữ nguyên (đã ghi ra file), _spawnedThisSession phải về 0.
            SceneLoader.Instance.ReloadScene();
        }

        private void RefreshStatus()
        {
            if (_statusText == null) return;

            _statusText.text =
                $"Coins (đã lưu ra file): {SaveManager.Data.coins}\n" +
                $"High score (đã lưu): {SaveManager.Data.highScore}\n" +
                $"Spawn trong phiên này: {_spawnedThisSession}\n" +
                $"Cube rảnh trong pool: {ObjectPool.Instance.CountInactive(CubeKey)}\n" +
                $"Mute: {AudioManager.Instance.IsMuted}\n\n" +
                $"Save file: {SaveManager.FilePath}";

            if (_muteButtonLabel != null)
                _muteButtonLabel.text = AudioManager.Instance.IsMuted ? "Bật tiếng" : "Tắt tiếng";
        }

        // ---------- Dựng nội dung bằng code ----------

        private static GameObject CreateCubePrefab()
        {
            // "Prefab" ở đây chỉ là 1 GameObject mẫu, tắt sẵn — ObjectPool Instantiate từ nó.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "DemoCube";
            cube.AddComponent<Rigidbody>();
            ApplyUrpMaterial(cube, new Color(1f, 0.78f, 0.25f));
            cube.SetActive(false);
            return cube;
        }

        private static void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            ApplyUrpMaterial(ground, new Color(0.2f, 0.22f, 0.28f));
        }

        /// <summary>
        /// Gán material URP tường minh. Project này chạy URP, primitive tạo lúc runtime mà dính
        /// material built-in sẽ hiện màu hồng "shader lỗi" — mất công nghi ngờ nhầm sang chỗ khác.
        /// </summary>
        private static void ApplyUrpMaterial(GameObject go, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;

            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            renderer.sharedMaterial = new Material(shader) { color = color };
        }

        /// <summary>Sinh 1 nốt sine bằng code để demo không cần file .wav nào.</summary>
        private static AudioClip CreateTone(string name, float frequency, float duration, float volume)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                // Fade 2 đầu để không bị tiếng "tách" khi bắt đầu/kết thúc sóng.
                float envelope = Mathf.Min(1f, Mathf.Min(i, sampleCount - i) / (sampleRate * 0.01f));
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * envelope;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            // Project đang để Active Input Handling = Input System Package (New),
            // nên phải dùng InputSystemUIInputModule; StandaloneInputModule sẽ ném lỗi runtime.
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetParent(null);
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;

            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 2f, -10f);
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("DemoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var column = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            column.SetParent(canvasGo.transform, false);
            column.anchorMin = new Vector2(0f, 1f);
            column.anchorMax = new Vector2(0f, 1f);
            column.pivot = new Vector2(0f, 1f);
            column.anchoredPosition = new Vector2(40f, -40f);
            column.sizeDelta = new Vector2(420f, 0f);

            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            CreateButton(column, "Spawn 1 cube (pool + SFX)", SpawnCube);
            CreateButton(column, "Spawn 20 cube 1 lúc", SpawnBurst);
            _muteButtonLabel = CreateButton(column, "Tắt tiếng", ToggleMute);
            CreateButton(column, "+10 coins (ghi ra file)", AddCoins);
            CreateButton(column, "Xoá save", DeleteSave);
            CreateButton(column, "Reload scene (màn loading)", ReloadScene);

            var statusRect = new GameObject("Status", typeof(RectTransform)).GetComponent<RectTransform>();
            statusRect.SetParent(canvasGo.transform, false);
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 40f);
            statusRect.sizeDelta = new Vector2(-80f, 300f);

            _statusText = statusRect.gameObject.AddComponent<Text>();
            _statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _statusText.fontSize = 26;
            _statusText.color = Color.white;
            _statusText.alignment = TextAnchor.LowerLeft;
            _statusText.raycastTarget = false;
        }

        private static Text CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(420f, 64f);

            go.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 0.95f);
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var textRect = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textRect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = label;

            return text;
        }

        private void Update()
        {
            // Số cube rảnh trong pool đổi liên tục do Return có delay — cập nhật nhẹ nhàng mỗi 0.25s.
            if (Time.frameCount % 15 == 0) RefreshStatus();
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using HC.Core;

namespace HC.UI
{
    /// <summary>
    /// Nút tạm dừng + bảng Pause/Settings. Dựng bằng code, không cần prefab — gọi phát chạy.
    ///
    /// Đây là bản khung cho gray-box. Sang Giai đoạn 4 thì thay phần <see cref="BuildUI"/> bằng
    /// UI thật, giữ nguyên phần logic bên dưới.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private Color panelColor = new Color(0.06f, 0.07f, 0.1f, 0.92f);
        [SerializeField] private Color buttonColor = new Color(0.16f, 0.18f, 0.24f, 1f);
        [SerializeField] private Color accentColor = new Color(1f, 0.78f, 0.25f, 1f);

        /// <summary>Bắn ra mỗi lần vào/ra pause. Gameplay nghe cái này để tự khoá input.</summary>
        public event Action<bool> OnPauseChanged;

        public bool IsPaused { get; private set; }

        private Canvas _canvas;
        private GameObject _panel;
        private GameObject _pauseButton;
        private Text _musicLabel;
        private Text _sfxLabel;
        private Font _font;

        // Pause menu set timeScale = 0, nhưng game có thể đang chạy slow-motion 0.3 — nhớ lại
        // giá trị cũ thay vì mặc định trả về 1.
        private float _timeScaleBeforePause = 1f;

        private static PauseMenu _instance;
        private static bool _isQuitting;

        public static PauseMenu Instance
        {
            get
            {
                if (_isQuitting) return null;
                if (_instance != null) return _instance;

                _instance = FindFirstObjectByType<PauseMenu>();
                if (_instance == null)
                {
                    var go = new GameObject("[PauseMenu]");
                    _instance = go.AddComponent<PauseMenu>();
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _isQuitting = false;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildUI();
            SetPanelVisible(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void Update()
        {
            // Phím Esc trên máy tính, và nút Back cứng trên Android đều vào đây.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Toggle();
        }

        private void OnApplicationPause(bool paused)
        {
            // Người chơi bấm Home giữa màn chơi: tự pause để lúc quay lại không bị chết oan.
            if (paused && !IsPaused) Pause();
        }

        // ---------- API ----------

        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;

            _timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            IsPaused = true;

            RefreshAudioLabels();
            SetPanelVisible(true);
            OnPauseChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!IsPaused) return;

            Time.timeScale = _timeScaleBeforePause;
            IsPaused = false;

            SetPanelVisible(false);
            OnPauseChanged?.Invoke(false);
        }

        /// <summary>Ẩn nút pause ở những scene không cần (menu chính, màn hình kết quả).</summary>
        public void ShowPauseButton(bool visible)
        {
            if (_pauseButton != null) _pauseButton.SetActive(visible);
        }

        private void Restart()
        {
            // Resume trước: SceneLoader cũng tự đặt lại timeScale, nhưng để trạng thái sạch ở đây
            // thì lỡ sau này đổi loader khác cũng không vỡ.
            Resume();
            SceneLoader.Instance.ReloadScene();
        }

        private void ToggleMusic()
        {
            AudioManager.Instance.ToggleMusic();
            RefreshAudioLabels();
        }

        private void ToggleSfx()
        {
            AudioManager.Instance.ToggleSfx();
            RefreshAudioLabels();
        }

        private void RefreshAudioLabels()
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;

            if (_musicLabel != null) _musicLabel.text = audio.IsMusicMuted ? "Nhạc: TẮT" : "Nhạc: BẬT";
            if (_sfxLabel != null) _sfxLabel.text = audio.IsSfxMuted ? "Âm thanh: TẮT" : "Âm thanh: BẬT";
        }

        private void SetPanelVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
            if (_pauseButton != null) _pauseButton.SetActive(!visible);
        }

        // ---------- Dựng UI ----------

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            // Project để Active Input Handling = Input System mới, StandaloneInputModule sẽ ném lỗi.
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000; // trên HUD gameplay, dưới màn hình loading (30000)

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            BuildPauseButton(canvasGo.transform);
            BuildPanel(canvasGo.transform);
        }

        private void BuildPauseButton(Transform parent)
        {
            _pauseButton = new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = _pauseButton.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-32f, -32f);
            rect.sizeDelta = new Vector2(96f, 96f);

            _pauseButton.GetComponent<Image>().color = buttonColor;
            _pauseButton.GetComponent<Button>().onClick.AddListener(Pause);

            CreateLabel(rect, "II", 40, accentColor);
        }

        private void BuildPanel(Transform parent)
        {
            _panel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
            var panelRect = _panel.GetComponent<RectTransform>();
            panelRect.SetParent(parent, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var background = _panel.GetComponent<Image>();
            background.color = panelColor;
            background.raycastTarget = true; // chặn tay chạm xuyên xuống gameplay bên dưới

            var column = new GameObject("Column", typeof(RectTransform), typeof(VerticalLayoutGroup))
                         .GetComponent<RectTransform>();
            column.SetParent(panelRect, false);
            column.anchorMin = new Vector2(0.5f, 0.5f);
            column.anchorMax = new Vector2(0.5f, 0.5f);
            column.pivot = new Vector2(0.5f, 0.5f);
            column.sizeDelta = new Vector2(560f, 0f);

            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            CreateTitle(column, "TẠM DỪNG");
            CreateButton(column, "Tiếp tục", Resume, accentColor);
            CreateButton(column, "Chơi lại", Restart, null);
            _musicLabel = CreateButton(column, "Nhạc", ToggleMusic, null);
            _sfxLabel = CreateButton(column, "Âm thanh", ToggleSfx, null);
        }

        private void CreateTitle(Transform parent, string text)
        {
            var rect = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(560f, 90f);

            var label = rect.gameObject.AddComponent<Text>();
            label.font = _font;
            label.fontSize = 48;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = text;
        }

        private Text CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick, Color? tint)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(560f, 96f);

            go.GetComponent<Image>().color = tint ?? buttonColor;
            go.GetComponent<Button>().onClick.AddListener(onClick);

            return CreateLabel(rect, text, 32, tint.HasValue ? Color.black : Color.white);
        }

        private Text CreateLabel(RectTransform parent, string text, int size, Color color)
        {
            var rect = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = rect.gameObject.AddComponent<Text>();
            label.font = _font;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.raycastTarget = false;
            label.text = text;

            return label;
        }
    }
}

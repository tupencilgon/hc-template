using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HC.Core
{
    /// <summary>
    /// Load scene bất đồng bộ kèm màn hình loading (Canvas + progress bar dựng bằng code, không cần prefab).
    /// Singleton — truy cập qua <see cref="Instance"/>, tự tạo nếu chưa có trong scene, sống qua các scene.
    ///
    /// Màn hình loading ở đây cố tình làm tối giản, đủ dùng cho gray-box. Sang Giai đoạn 4 thì thay
    /// phần dựng UI trong <see cref="BuildLoadingScreen"/> bằng UI thật do GPT thiết kế.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SceneLoader : MonoBehaviour
    {
        [Header("Thời lượng")]
        [Tooltip("Thời gian fade vào/ra màn hình loading (giây).")]
        [Min(0f)][SerializeField] private float fadeDuration = 0.25f;
        [Tooltip("Thời gian tối thiểu giữ màn hình loading (giây). Scene nhẹ load xong trong 2 frame, " +
                 "không có mốc này thì màn loading chớp một cái rất khó chịu.")]
        [Min(0f)][SerializeField] private float minimumDuration = 0.6f;
        [Tooltip("Tốc độ bar chạy tối đa (phần trăm/giây, 1 = đầy bar mất 1 giây).")]
        [Min(0.1f)][SerializeField] private float barFillSpeed = 1.5f;

        [Header("Giao diện")]
        [SerializeField] private Color backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
        [SerializeField] private Color barTrackColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color barFillColor = new Color(1f, 0.78f, 0.25f, 1f);
        [SerializeField] private bool showPercentText = true;

        [Header("Hành vi")]
        [Tooltip("Đặt lại Time.timeScale = 1 khi scene mới vào. Pause menu set timeScale = 0 rồi bấm " +
                 "Restart mà không reset thì scene mới đứng hình.")]
        [SerializeField] private bool resetTimeScaleOnLoad = true;

        /// <summary>Tiến độ 0..1, bắn ra mỗi frame lúc đang load (dùng cho UI loading riêng nếu cần).</summary>
        public event Action<float> OnProgress;

        public bool IsLoading { get; private set; }

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _barFill;
        private Text _percentText;

        private static SceneLoader _instance;
        private static bool _isQuitting;

        public static SceneLoader Instance
        {
            get
            {
                if (_isQuitting) return null;
                if (_instance != null) return _instance;

                _instance = FindFirstObjectByType<SceneLoader>();
                if (_instance == null)
                {
                    var go = new GameObject("[SceneLoader]");
                    _instance = go.AddComponent<SceneLoader>();
                }
                return _instance;
            }
        }

        // Enter Play Mode không reload domain => static không tự reset. Dòng này lo việc đó.
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

            BuildLoadingScreen();
            SetScreenVisible(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        /// <summary>Load scene theo tên (tên phải có trong Build Settings).</summary>
        public void LoadScene(string sceneName)
        {
            LoadScene(sceneName, null);
        }

        /// <param name="onComplete">Gọi sau khi scene mới đã vào và màn loading đã fade hết.</param>
        public void LoadScene(string sceneName, Action onComplete)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] Đang load scene khác rồi — bỏ qua yêu cầu load \"{sceneName}\".");
                return;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneLoader] Tên scene rỗng.");
                return;
            }

            // Không check trước thì LoadSceneAsync ném lỗi khó hiểu, rất tốn thời gian mò.
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene \"{sceneName}\" chưa có trong Build Settings " +
                               "(File > Build Profiles > Scene List). Không load được.");
                return;
            }

            StartCoroutine(LoadRoutine(sceneName, onComplete));
        }

        /// <summary>Load lại scene đang chạy — dùng cho nút Restart/Retry.</summary>
        public void ReloadScene(Action onComplete = null)
        {
            LoadScene(SceneManager.GetActiveScene().name, onComplete);
        }

        private IEnumerator LoadRoutine(string sceneName, Action onComplete)
        {
            IsLoading = true;

            SetScreenVisible(true);
            SetProgress(0f);
            yield return FadeRoutine(0f, 1f);

            var op = SceneManager.LoadSceneAsync(sceneName);
            // Giữ scene mới ở trạng thái chờ để bar kịp chạy hết, thay vì nhảy phịch sang scene mới.
            op.allowSceneActivation = false;

            float startedAt = Time.unscaledTime;
            float displayed = 0f;

            while (true)
            {
                // LoadSceneAsync chỉ chạy progress tới 0.9 rồi đứng im chờ allowSceneActivation.
                // Không quy đổi lại thì bar không bao giờ đầy.
                float target = Mathf.Clamp01(op.progress / 0.9f);

                if (minimumDuration > 0f)
                {
                    float byTime = (Time.unscaledTime - startedAt) / minimumDuration;
                    target = Mathf.Min(target, byTime);
                }

                // unscaledDeltaTime: pause menu set timeScale = 0 thì deltaTime thường đứng, bar kẹt mãi.
                displayed = Mathf.MoveTowards(displayed, target, Time.unscaledDeltaTime * barFillSpeed);
                SetProgress(displayed);

                if (op.progress >= 0.9f && displayed >= 0.999f) break;
                yield return null;
            }

            SetProgress(1f);
            if (resetTimeScaleOnLoad) Time.timeScale = 1f;

            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            yield return FadeRoutine(1f, 0f);
            SetScreenVisible(false);

            IsLoading = false;
            onComplete?.Invoke();
        }

        private IEnumerator FadeRoutine(float from, float to)
        {
            if (fadeDuration <= 0f)
            {
                _group.alpha = to;
                yield break;
            }

            float t = 0f;
            _group.alpha = from;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, t / fadeDuration);
                yield return null;
            }
            _group.alpha = to;
        }

        private void SetProgress(float progress01)
        {
            progress01 = Mathf.Clamp01(progress01);

            if (_barFill != null)
            {
                // Kéo bằng anchor thay vì Image.fillAmount: fillAmount cần sprite, anchor thì không.
                var max = _barFill.anchorMax;
                max.x = progress01;
                _barFill.anchorMax = max;
            }

            if (_percentText != null) _percentText.text = Mathf.RoundToInt(progress01 * 100f) + "%";

            OnProgress?.Invoke(progress01);
        }

        private void SetScreenVisible(bool visible)
        {
            if (_canvas != null) _canvas.enabled = visible;
            if (_group == null) return;

            if (!visible) _group.alpha = 0f;
            _group.blocksRaycasts = visible;
        }

        /// <summary>Dựng Canvas + progress bar bằng code. Thay cả hàm này khi có UI thật.</summary>
        private void BuildLoadingScreen()
        {
            var canvasGo = new GameObject("LoadingCanvas", typeof(Canvas), typeof(CanvasScaler),
                                          typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30000; // phải nằm trên mọi UI của gameplay

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = canvasGo.GetComponent<CanvasGroup>();
            _group.alpha = 0f;

            var background = CreateRect("Background", canvasGo.transform);
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;
            // Chỉ riêng nền bật raycastTarget: đây là thứ chặn tay người chơi chạm xuyên xuống scene bên dưới.
            AddImage(background, backgroundColor, blockRaycast: true);

            var track = CreateRect("BarTrack", canvasGo.transform);
            track.anchorMin = new Vector2(0.5f, 0.5f);
            track.anchorMax = new Vector2(0.5f, 0.5f);
            track.pivot = new Vector2(0.5f, 0.5f);
            track.sizeDelta = new Vector2(600f, 24f);
            track.anchoredPosition = new Vector2(0f, -120f);
            AddImage(track, barTrackColor);

            _barFill = CreateRect("BarFill", track);
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = new Vector2(0f, 1f); // x sẽ chạy 0 -> 1 theo tiến độ
            _barFill.pivot = new Vector2(0f, 0.5f);
            _barFill.offsetMin = Vector2.zero;
            _barFill.offsetMax = Vector2.zero;
            AddImage(_barFill, barFillColor);

            if (showPercentText) BuildPercentText(track);
        }

        private void BuildPercentText(RectTransform track)
        {
            // Font dựng sẵn của Unity: không cần import asset nào, đổi lại là không kiểm soát được kiểu chữ.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return;

            var textRect = CreateRect("PercentText", track);
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.offsetMin = new Vector2(0f, 16f);
            textRect.offsetMax = new Vector2(0f, 64f);

            _percentText = textRect.gameObject.AddComponent<Text>();
            _percentText.font = font;
            _percentText.fontSize = 32;
            _percentText.alignment = TextAnchor.MiddleCenter;
            _percentText.color = new Color(1f, 1f, 1f, 0.7f);
            _percentText.raycastTarget = false;
            _percentText.text = "0%";
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void AddImage(RectTransform rect, Color color, bool blockRaycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = blockRaycast;
        }
    }
}

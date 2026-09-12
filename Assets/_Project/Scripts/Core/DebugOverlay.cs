using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HC.Core
{
    /// <summary>
    /// Bảng số liệu hiệu năng: FPS hiện tại, FPS thấp nhất, draw call, SetPass call, tam giác, bộ nhớ.
    ///
    /// Vì sao món này quan trọng với hypercasual: retention phụ thuộc trực tiếp vào việc game có mượt
    /// trên máy tầm trung hay không. Máy dev thì game nào cũng 60 FPS — con số đáng tin duy nhất là
    /// con số đo trên chính cái điện thoại rẻ tiền. Nên overlay này phải bật được ngay trên máy thật,
    /// không cần cắm dây hay mở profiler.
    ///
    /// Bật/tắt: chạm 3 lần vào góc trên bên trái màn hình, hoặc bấm F3.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class DebugOverlay : MonoBehaviour
    {
        private const float SampleWindow = 0.5f;
        private const int CornerTapsToToggle = 3;
        private const float CornerTapTimeout = 1.5f;
        private const float CornerSizeRatio = 0.18f;

        [Tooltip("Tự hiện lúc khởi động (chỉ ở Editor và development build).")]
        [SerializeField] private bool visibleOnStart = true;

        public bool IsVisible { get; private set; }

        private float _accumulatedTime;
        private int _accumulatedFrames;
        private float _currentFps;
        private float _worstFps = float.MaxValue;

        private int _cornerTaps;
        private float _lastCornerTapAt;

        private GUIStyle _style;
        private Texture2D _background;
        private readonly StringBuilder _text = new StringBuilder(256);

        private ProfilerRecorder _drawCalls;
        private ProfilerRecorder _setPassCalls;
        private ProfilerRecorder _triangles;
        private ProfilerRecorder _memory;

        private static DebugOverlay _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        /// <summary>
        /// Tự dựng lúc mở game. Chỉ chạy ở Editor và development build — bản release không có gì
        /// để quên tắt, và số liệu profiler cũng không tồn tại ở bản release.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_instance != null) return;

            var go = new GameObject("[DebugOverlay]");
            _instance = go.AddComponent<DebugOverlay>();
#endif
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

            IsVisible = visibleOnStart;
        }

        private void OnEnable()
        {
            // ProfilerRecorder đọc thẳng số liệu của engine, chạy được cả trên development build
            // trên máy thật — khác UnityEditor.UnityStats vốn chỉ tồn tại trong Editor.
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
        }

        private void OnDisable()
        {
            _drawCalls.Dispose();
            _setPassCalls.Dispose();
            _triangles.Dispose();
            _memory.Dispose();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_background != null) Destroy(_background);
        }

        private void Update()
        {
            TrackFps();
            TrackToggleInput();
        }

        private void TrackFps()
        {
            // unscaledDeltaTime: lúc pause timeScale = 0, dùng deltaTime thường thì FPS hiện 0.
            _accumulatedTime += Time.unscaledDeltaTime;
            _accumulatedFrames++;

            if (_accumulatedTime < SampleWindow) return;

            _currentFps = _accumulatedFrames / _accumulatedTime;
            _accumulatedTime = 0f;
            _accumulatedFrames = 0;

            // Bỏ qua vài giây đầu: lúc mới mở game còn đang nạp asset, FPS thấp là chuyện đương nhiên
            // và không phản ánh gì về gameplay.
            if (Time.unscaledTime > 3f && _currentFps < _worstFps) _worstFps = _currentFps;
        }

        private void TrackToggleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
            {
                IsVisible = !IsVisible;
                return;
            }

            // Pointer bao cả chuột lẫn cảm ứng, nên cùng một đoạn chạy được trên máy tính và điện thoại.
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            Vector2 position = pointer.position.ReadValue();
            bool inCorner = position.x < Screen.width * CornerSizeRatio &&
                            position.y > Screen.height * (1f - CornerSizeRatio);

            if (!inCorner) return;

            if (Time.unscaledTime - _lastCornerTapAt > CornerTapTimeout) _cornerTaps = 0;

            _lastCornerTapAt = Time.unscaledTime;
            _cornerTaps++;

            if (_cornerTaps < CornerTapsToToggle) return;

            _cornerTaps = 0;
            IsVisible = !IsVisible;
        }

        /// <summary>Đặt lại mốc FPS thấp nhất. Gọi khi bắt đầu một màn chơi để đo riêng màn đó.</summary>
        public void ResetWorstFps()
        {
            _worstFps = float.MaxValue;
        }

        private void OnGUI()
        {
            if (!IsVisible) return;

            EnsureStyle();

            _text.Clear();
            _text.Append("FPS  ").Append(Mathf.RoundToInt(_currentFps));
            _text.Append("   thấp nhất ").Append(_worstFps < float.MaxValue ? Mathf.RoundToInt(_worstFps) : 0);
            _text.Append('\n');

            AppendStat("Draw call ", _drawCalls);
            AppendStat("SetPass   ", _setPassCalls);
            AppendStat("Tam giác  ", _triangles);

            if (_memory.Valid)
            {
                _text.Append("Bộ nhớ    ").Append(_memory.LastValue / (1024 * 1024)).Append(" MB\n");
            }

            _text.Append("(overlay tự nó tốn ~2 draw call)");

            float width = 340f * Mathf.Max(1f, Screen.width / 1080f);
            GUI.Label(new Rect(12f, 12f, width, 190f), _text.ToString(), _style);
        }

        private void AppendStat(string label, ProfilerRecorder recorder)
        {
            _text.Append(label);

            // Bản release không bật số liệu profiler — nói thẳng thay vì hiện số 0 gây hiểu nhầm.
            if (recorder.Valid) _text.Append(recorder.LastValue);
            else _text.Append("n/a");

            _text.Append('\n');
        }

        private void EnsureStyle()
        {
            // Tạo GUIStyle trong OnGUI mỗi frame là sinh rác GC liên tục — đúng thứ overlay này
            // sinh ra để phát hiện. Tạo một lần rồi dùng lại.
            if (_style != null) return;

            _background = new Texture2D(1, 1);
            _background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            _background.Apply();

            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18f * Mathf.Max(1f, Screen.width / 1080f)),
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 8, 8),
                richText = false
            };

            _style.normal.textColor = new Color(0.75f, 1f, 0.6f);
            _style.normal.background = _background;
        }
    }
}

using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HC.Analytics
{
    /// <summary>
    /// Cửa duy nhất để bắn event analytics. Gameplay chỉ gọi class này, không gọi SDK.
    ///
    /// Bộ event chuẩn (dùng chung cho MỌI game để so sánh số liệu giữa các game với nhau):
    /// level_start · level_complete · level_fail · ad_shown · ad_reward_claimed
    ///
    /// Event bắn trước khi SDK init xong sẽ được xếp hàng chờ rồi gửi bù, không mất —
    /// SDK thật init mất 1-3 giây, mà level_start thường bắn ngay giây đầu.
    /// </summary>
    public static class AnalyticsManager
    {
        // Firebase giới hạn 40 ký tự, chỉ chữ/số/gạch dưới, phải bắt đầu bằng chữ cái.
        // Sai luật thì event bị bỏ lặng lẽ ở server — không lỗi, không log, chỉ là không có số liệu.
        private const int MaxNameLength = 40;
        private const int MaxStringValueLength = 100;
        private const int MaxQueuedEvents = 100;

        private static readonly Regex ValidName = new Regex("^[A-Za-z][A-Za-z0-9_]*$");
        private static readonly List<IAnalyticsProvider> Providers = new List<IAnalyticsProvider>();
        private static readonly Queue<QueuedEvent> Pending = new Queue<QueuedEvent>();

        private static bool _initialized;
        private static bool _consentGranted = true;

        private readonly struct QueuedEvent
        {
            public readonly string Name;
            public readonly Dictionary<string, object> Parameters;

            public QueuedEvent(string name, Dictionary<string, object> parameters)
            {
                Name = name;
                Parameters = parameters;
            }
        }

        /// <summary>Bật log ra Console mỗi lần bắn event. Nên bật lúc dev, tắt ở bản release.</summary>
        public static bool VerboseLogging { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Providers.Clear();
            Pending.Clear();
            _initialized = false;
            _consentGranted = true;
        }

        /// <summary>
        /// Đăng ký 1 nơi nhận event. Gọi ở bootstrap, trước khi gameplay bắt đầu.
        /// Chưa đăng ký provider nào thì event vẫn được kiểm tra hợp lệ và log ra Console.
        /// </summary>
        public static void AddProvider(IAnalyticsProvider provider)
        {
            if (provider == null) return;
            if (Providers.Contains(provider)) return;

            Providers.Add(provider);
            if (_initialized) provider.Initialize();
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            for (int i = 0; i < Providers.Count; i++) Providers[i].Initialize();
        }

        /// <summary>
        /// Đặt trạng thái đồng ý thu thập dữ liệu. Bắt buộc phải xử lý đúng nếu phát hành ở EU.
        /// Publisher thường yêu cầu game gọi hàm này từ màn hình consent của họ.
        /// </summary>
        public static void SetConsent(bool granted)
        {
            _consentGranted = granted;
            for (int i = 0; i < Providers.Count; i++) Providers[i].SetConsent(granted);
        }

        public static void SetUserProperty(string key, string value)
        {
            if (!_consentGranted) return;
            if (!IsNameValid(key, "user property")) return;

            for (int i = 0; i < Providers.Count; i++) Providers[i].SetUserProperty(key, Truncate(value));
        }

        // ---------- Bộ event chuẩn ----------

        /// <summary>Bắn ngay khi màn chơi bắt đầu chạy, không phải lúc bấm nút Play ở menu.</summary>
        public static void LevelStart(int levelIndex, string levelName = null)
        {
            var p = NewParams();
            p["level_index"] = levelIndex;
            if (!string.IsNullOrEmpty(levelName)) p["level_name"] = levelName;
            LogEvent("level_start", p);
        }

        /// <param name="durationSeconds">Thời gian chơi màn đó. Đây là số dùng để tìm màn nào quá dài/quá dễ.</param>
        public static void LevelComplete(int levelIndex, float durationSeconds, int score = 0)
        {
            var p = NewParams();
            p["level_index"] = levelIndex;
            p["duration"] = Mathf.RoundToInt(durationSeconds);
            p["score"] = score;
            LogEvent("level_complete", p);
        }

        /// <param name="reason">Lý do thua, vd: "hit_obstacle", "timeout". Dùng để tìm chỗ gây ức chế.</param>
        public static void LevelFail(int levelIndex, float durationSeconds, string reason = null)
        {
            var p = NewParams();
            p["level_index"] = levelIndex;
            p["duration"] = Mathf.RoundToInt(durationSeconds);
            if (!string.IsNullOrEmpty(reason)) p["reason"] = reason;
            LogEvent("level_fail", p);
        }

        /// <param name="adType">"interstitial" | "rewarded" | "banner"</param>
        /// <param name="placement">Vị trí trong game, vd: "level_end", "revive". Đừng đặt tên kiểu "ad_1".</param>
        public static void AdShown(string adType, string placement)
        {
            var p = NewParams();
            p["ad_type"] = adType;
            p["placement"] = placement;
            LogEvent("ad_shown", p);
        }

        /// <summary>Bắn khi người chơi XEM HẾT rewarded và nhận thưởng — không phải lúc bấm nút xem.</summary>
        public static void AdRewardClaimed(string placement)
        {
            var p = NewParams();
            p["placement"] = placement;
            LogEvent("ad_reward_claimed", p);
        }

        // ---------- Event tự do ----------

        public static void LogEvent(string eventName)
        {
            LogEvent(eventName, null);
        }

        public static void LogEvent(string eventName, string paramName, object paramValue)
        {
            var p = NewParams();
            p[paramName] = paramValue;
            LogEvent(eventName, p);
        }

        public static void LogEvent(string eventName, Dictionary<string, object> parameters)
        {
            if (!_consentGranted) return;
            if (!IsNameValid(eventName, "event")) return;

            if (parameters != null) SanitizeParameters(eventName, parameters);

            if (VerboseLogging) Debug.Log($"[Analytics] {eventName} {Describe(parameters)}");

            bool deliveredToSomeone = false;

            for (int i = 0; i < Providers.Count; i++)
            {
                if (!Providers[i].IsReady) continue;
                Providers[i].LogEvent(eventName, parameters);
                deliveredToSomeone = true;
            }

            // Chưa provider nào sẵn sàng => xếp hàng chờ, gửi bù trong Flush().
            if (!deliveredToSomeone && Providers.Count > 0) Enqueue(eventName, parameters);
        }

        /// <summary>
        /// Gửi bù các event đã xếp hàng. Gọi khi provider báo init xong.
        /// </summary>
        public static void Flush()
        {
            if (Pending.Count == 0) return;

            int count = Pending.Count;
            for (int i = 0; i < count; i++)
            {
                var queued = Pending.Dequeue();
                bool delivered = false;

                for (int p = 0; p < Providers.Count; p++)
                {
                    if (!Providers[p].IsReady) continue;
                    Providers[p].LogEvent(queued.Name, queued.Parameters);
                    delivered = true;
                }

                // Vẫn chưa ai sẵn sàng — trả lại hàng đợi, lần Flush sau thử tiếp.
                if (!delivered) Pending.Enqueue(queued);
            }
        }

        // ---------- Kiểm tra hợp lệ ----------

        private static Dictionary<string, object> NewParams()
        {
            return new Dictionary<string, object>(4);
        }

        private static void Enqueue(string eventName, Dictionary<string, object> parameters)
        {
            // Chặn hàng đợi phình vô hạn khi SDK không bao giờ init được (mất mạng, thiếu config).
            if (Pending.Count >= MaxQueuedEvents) Pending.Dequeue();
            Pending.Enqueue(new QueuedEvent(eventName, parameters));
        }

        private static bool IsNameValid(string name, string kind)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogError($"[Analytics] Tên {kind} rỗng — bỏ qua.");
                return false;
            }

            if (name.Length > MaxNameLength)
            {
                Debug.LogError($"[Analytics] Tên {kind} \"{name}\" dài quá {MaxNameLength} ký tự — Firebase sẽ bỏ lặng lẽ.");
                return false;
            }

            if (!ValidName.IsMatch(name))
            {
                Debug.LogError($"[Analytics] Tên {kind} \"{name}\" sai định dạng — chỉ dùng chữ/số/gạch dưới và phải bắt đầu bằng chữ cái.");
                return false;
            }

            // Firebase chặn cứng mấy tiền tố này, dùng vào là event biến mất không dấu vết.
            string lower = name.ToLowerInvariant();
            if (lower.StartsWith("firebase_") || lower.StartsWith("google_") || lower.StartsWith("ga_"))
            {
                Debug.LogError($"[Analytics] Tên {kind} \"{name}\" dùng tiền tố dành riêng của Firebase — đổi tên khác.");
                return false;
            }

            return true;
        }

        private static void SanitizeParameters(string eventName, Dictionary<string, object> parameters)
        {
            List<string> invalidKeys = null;

            foreach (var pair in parameters)
            {
                if (IsNameValid(pair.Key, $"tham số của \"{eventName}\"")) continue;
                invalidKeys ??= new List<string>();
                invalidKeys.Add(pair.Key);
            }

            if (invalidKeys != null)
            {
                for (int i = 0; i < invalidKeys.Count; i++) parameters.Remove(invalidKeys[i]);
            }

            // Cắt chuỗi quá dài thay vì để cả event bị loại.
            List<string> longKeys = null;
            foreach (var pair in parameters)
            {
                if (pair.Value is string s && s.Length > MaxStringValueLength)
                {
                    longKeys ??= new List<string>();
                    longKeys.Add(pair.Key);
                }
            }

            if (longKeys == null) return;
            for (int i = 0; i < longKeys.Count; i++)
            {
                parameters[longKeys[i]] = Truncate((string)parameters[longKeys[i]]);
            }
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= MaxStringValueLength ? value : value.Substring(0, MaxStringValueLength);
        }

        private static string Describe(Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0) return "{}";

            var sb = new System.Text.StringBuilder("{");
            bool first = true;
            foreach (var pair in parameters)
            {
                if (!first) sb.Append(", ");
                sb.Append(pair.Key).Append('=').Append(pair.Value);
                first = false;
            }
            return sb.Append('}').ToString();
        }
    }
}

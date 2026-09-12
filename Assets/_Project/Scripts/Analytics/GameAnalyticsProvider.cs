using System.Collections.Generic;
using UnityEngine;
#if HC_GAMEANALYTICS
using GameAnalyticsSDK;
#endif

namespace HC.Analytics
{
    /// <summary>
    /// Gửi event sang GameAnalytics.
    ///
    /// Khác Firebase ở một điểm quan trọng: GameAnalytics KHÔNG nhận event tự do rồi tự dựng báo cáo.
    /// Dashboard của nó (phễu theo màn chơi, báo cáo doanh thu quảng cáo) chỉ hoạt động khi mình bắn
    /// đúng <em>loại</em> event nó định nghĩa sẵn — progression event và ad event. Bắn tất cả dưới dạng
    /// design event thì vẫn có số, nhưng mất sạch phần báo cáo dựng sẵn, tức là mất lý do dùng GameAnalytics.
    ///
    /// Nên provider này dịch 5 event chuẩn của mình sang đúng loại event của GameAnalytics.
    ///
    /// Class luôn compile được kể cả khi chưa import SDK — xem <c>SdkDefines.cs</c>.
    /// </summary>
    public class GameAnalyticsProvider : IAnalyticsProvider
    {
        /// <summary>
        /// Tên mediation báo cho GameAnalytics. Đổi thành "max" khi cắm AppLovin MAX.
        /// </summary>
        public static string AdSdkName = "none";

        public string Name => "GameAnalytics";

        public bool IsReady { get; private set; }

        private bool _warnedAboutUserProperty;

        public void Initialize()
        {
#if HC_GAMEANALYTICS
            if (GameAnalytics.Initialized)
            {
                MarkReady();
                return;
            }

            GameAnalytics.onInitialize += OnGameAnalyticsInitialized;
            GameAnalytics.Initialize();
#else
            Debug.LogWarning("[Analytics/GameAnalytics] Chưa import GameAnalytics SDK — provider đứng yên. " +
                             "Xem Docs/setup-may-moi.md.");
#endif
        }

#if HC_GAMEANALYTICS
        private void OnGameAnalyticsInitialized(object sender, bool success)
        {
            GameAnalytics.onInitialize -= OnGameAnalyticsInitialized;

            if (!success)
            {
                Debug.LogError("[Analytics/GameAnalytics] Khởi tạo thất bại. Kiểm tra Game Key / Secret Key " +
                               "trong Window > GameAnalytics > Select Settings.");
                return;
            }

            MarkReady();
        }

        private void MarkReady()
        {
            IsReady = true;
            Debug.Log("[Analytics/GameAnalytics] Sẵn sàng.");
            AnalyticsManager.Flush();
        }
#endif

        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
#if HC_GAMEANALYTICS
            switch (eventName)
            {
                case "level_start":
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, LevelId(parameters));
                    return;

                case "level_complete":
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, LevelId(parameters),
                                                      GetInt(parameters, "score"));
                    return;

                case "level_fail":
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, LevelId(parameters),
                                                      GetInt(parameters, "score"));
                    return;

                case "ad_shown":
                    GameAnalytics.NewAdEvent(GAAdAction.Show,
                                             ToAdType(GetString(parameters, "ad_type")),
                                             AdSdkName,
                                             GetString(parameters, "placement", "unknown"));
                    return;

                case "ad_reward_claimed":
                    GameAnalytics.NewAdEvent(GAAdAction.RewardReceived,
                                             GAAdType.RewardedVideo,
                                             AdSdkName,
                                             GetString(parameters, "placement", "unknown"));
                    return;

                default:
                    // Event riêng của từng game. GameAnalytics gom chúng vào mục Design events.
                    GameAnalytics.NewDesignEvent(eventName);
                    return;
            }
#endif
        }

        public void SetUserProperty(string key, string value)
        {
            // GameAnalytics không có thuộc tính người chơi tự do như Firebase. Nó chỉ có đúng 3 ô
            // "custom dimension", và giá trị hợp lệ phải khai báo trước trên dashboard — nhét bừa
            // vào đây là event bị loại. Ai cần thì gọi thẳng GameAnalytics.SetCustomDimension01/02/03.
            if (_warnedAboutUserProperty) return;
            _warnedAboutUserProperty = true;

            Debug.Log($"[Analytics/GameAnalytics] Bỏ qua user property \"{key}\" — GameAnalytics chỉ hỗ trợ " +
                      "3 custom dimension khai báo sẵn trên dashboard. Firebase vẫn nhận bình thường.");
        }

        public void SetConsent(bool granted)
        {
#if HC_GAMEANALYTICS
            GameAnalytics.SetEnabledEventSubmission(granted);
#endif
        }

#if HC_GAMEANALYTICS
        // ---------- Đọc tham số ----------

        /// <summary>
        /// GameAnalytics dùng CHUỖI làm mã màn chơi, không phải số. Chuẩn hoá về "level_07" để
        /// dashboard sắp xếp đúng thứ tự — để "level_7" thì nó xếp sau "level_10" theo alphabet.
        /// </summary>
        private static string LevelId(IReadOnlyDictionary<string, object> parameters)
        {
            int index = GetInt(parameters, "level_index");
            string name = GetString(parameters, "level_name");

            return string.IsNullOrEmpty(name) ? $"level_{index:D2}" : $"level_{index:D2}_{name}";
        }

        private static int GetInt(IReadOnlyDictionary<string, object> parameters, string key)
        {
            if (parameters == null) return 0;
            if (!parameters.TryGetValue(key, out var value)) return 0;

            return value switch
            {
                int v => v,
                long v => (int)v,
                float v => Mathf.RoundToInt(v),
                double v => (int)System.Math.Round(v),
                _ => 0
            };
        }

        private static string GetString(IReadOnlyDictionary<string, object> parameters, string key,
                                        string fallback = null)
        {
            if (parameters == null) return fallback;
            if (!parameters.TryGetValue(key, out var value) || value == null) return fallback;

            return value.ToString();
        }

        private static GAAdType ToAdType(string adType)
        {
            return adType switch
            {
                "interstitial" => GAAdType.Interstitial,
                "rewarded" => GAAdType.RewardedVideo,
                "banner" => GAAdType.Banner,
                "playable" => GAAdType.Playable,
                "app_open" => GAAdType.AppOpen,
                _ => GAAdType.Undefined
            };
        }
#endif
    }
}

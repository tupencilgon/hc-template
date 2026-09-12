using System.Collections.Generic;
using UnityEngine;
#if HC_FIREBASE
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
#endif

namespace HC.Analytics
{
    /// <summary>
    /// Gửi event sang Firebase Analytics.
    ///
    /// Class này LUÔN compile được, kể cả khi máy chưa import Firebase SDK — phần gọi SDK thật nằm
    /// sau <c>#if HC_FIREBASE</c>, và define đó do <c>SdkDefines.cs</c> tự bật/tắt theo việc SDK có
    /// mặt hay không. Nhờ vậy clone repo về máy mới (SDK không nằm trong Git) vẫn build được ngay.
    /// </summary>
    public class FirebaseAnalyticsProvider : IAnalyticsProvider
    {
        public string Name => "Firebase";

        public bool IsReady { get; private set; }

        private bool _consentGranted = true;

        public void Initialize()
        {
#if HC_FIREBASE
            // Firebase cần Google Play services đủ mới. Hàm này kiểm tra và tự vá nếu thiếu.
            // Nó chạy bất đồng bộ — đây chính là lý do AnalyticsManager phải xếp hàng chờ event.
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result != DependencyStatus.Available)
                {
                    Debug.LogError($"[Analytics/Firebase] Không khởi tạo được: {task.Result}. " +
                                   "Thường do thiếu google-services.json hoặc Google Play services quá cũ.");
                    return;
                }

                FirebaseAnalytics.SetAnalyticsCollectionEnabled(_consentGranted);
                IsReady = true;

                Debug.Log("[Analytics/Firebase] Sẵn sàng.");
                AnalyticsManager.Flush();
            });
#else
            Debug.LogWarning("[Analytics/Firebase] Chưa import Firebase SDK — provider đứng yên, " +
                             "không gửi gì cả. Xem Docs/setup-may-moi.md.");
#endif
        }

        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
#if HC_FIREBASE
            if (parameters == null || parameters.Count == 0)
            {
                FirebaseAnalytics.LogEvent(eventName);
                return;
            }

            var converted = new Parameter[parameters.Count];
            int i = 0;

            foreach (var pair in parameters)
            {
                // Firebase chỉ nhận 3 kiểu: string, long, double. Kiểu khác phải quy đổi,
                // không thì mất tham số mà không báo lỗi.
                converted[i++] = pair.Value switch
                {
                    int v => new Parameter(pair.Key, (long)v),
                    long v => new Parameter(pair.Key, v),
                    float v => new Parameter(pair.Key, (double)v),
                    double v => new Parameter(pair.Key, v),
                    bool v => new Parameter(pair.Key, v ? 1L : 0L),
                    null => new Parameter(pair.Key, string.Empty),
                    _ => new Parameter(pair.Key, pair.Value.ToString())
                };
            }

            FirebaseAnalytics.LogEvent(eventName, converted);
#endif
        }

        public void SetUserProperty(string key, string value)
        {
#if HC_FIREBASE
            FirebaseAnalytics.SetUserProperty(key, value);
#endif
        }

        public void SetConsent(bool granted)
        {
            _consentGranted = granted;
#if HC_FIREBASE
            if (IsReady) FirebaseAnalytics.SetAnalyticsCollectionEnabled(granted);
#endif
        }
    }
}

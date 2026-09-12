using System.Collections.Generic;
using UnityEngine;

namespace HC.Analytics
{
    /// <summary>
    /// Provider giả, chỉ in ra Console. Dùng lúc dev và lúc chưa cài SDK thật.
    ///
    /// Mục đích thật sự: cho phép gắn event vào gameplay NGAY BÂY GIỜ. Đến khi cắm Firebase/GameAnalytics
    /// thì chỉ thêm provider mới, không phải đi sửa lại từng chỗ gọi trong code gameplay.
    /// </summary>
    public class DebugAnalyticsProvider : IAnalyticsProvider
    {
        public string Name => "Debug";

        public bool IsReady { get; private set; }

        public void Initialize()
        {
            IsReady = true;
            Debug.Log("[Analytics/Debug] Sẵn sàng — event sẽ chỉ in ra Console, không gửi đi đâu cả.");
            AnalyticsManager.Flush();
        }

        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            // AnalyticsManager đã in event rồi khi VerboseLogging bật, nên ở đây không in lặp.
        }

        public void SetUserProperty(string key, string value)
        {
            Debug.Log($"[Analytics/Debug] user property {key} = {value}");
        }

        public void SetConsent(bool granted)
        {
            Debug.Log($"[Analytics/Debug] consent = {granted}");
        }
    }
}

using System.Collections.Generic;

namespace HC.Analytics
{
    /// <summary>
    /// Một "nơi nhận" event analytics (Firebase, GameAnalytics, console lúc dev...).
    ///
    /// Code gameplay KHÔNG bao giờ gọi thẳng SDK — chỉ gọi <see cref="AnalyticsManager"/>.
    /// Nhờ vậy đổi/thêm/bỏ nhà cung cấp analytics không phải sửa một dòng gameplay nào,
    /// và code vẫn compile + chạy được khi trong máy chưa cài SDK nào cả.
    /// </summary>
    public interface IAnalyticsProvider
    {
        /// <summary>Tên hiển thị trong log, vd: "Firebase".</summary>
        string Name { get; }

        /// <summary>Đã init xong và sẵn sàng nhận event chưa. SDK thật init bất đồng bộ nên có độ trễ.</summary>
        bool IsReady { get; }

        void Initialize();

        void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters);

        /// <summary>Thuộc tính gắn với người chơi, không phải với 1 event (vd: "player_level" = "12").</summary>
        void SetUserProperty(string key, string value);

        /// <summary>
        /// Đồng ý/từ chối thu thập dữ liệu (GDPR ở EU, CCPA ở California, ATT trên iOS).
        /// Chưa có consent thì provider phải tự ngừng gửi dữ liệu.
        /// </summary>
        void SetConsent(bool granted);
    }
}

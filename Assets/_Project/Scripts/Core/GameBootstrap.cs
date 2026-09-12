using UnityEngine;
using HC.Analytics;

namespace HC.Core
{
    /// <summary>
    /// Khởi động các hệ thống nền, tự chạy trước khi scene đầu tiên bắt đầu.
    /// Không cần gắn vào GameObject nào, không cần nhớ gọi ở đâu — kể cả khi bấm Play thẳng
    /// vào một scene giữa chừng lúc đang dev, mọi thứ vẫn được khởi tạo đúng.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // Bản release không in event ra Console nữa: vừa lộ cấu trúc dữ liệu, vừa tốn hiệu năng.
            AnalyticsManager.VerboseLogging = Debug.isDebugBuild;

            AnalyticsManager.AddProvider(new DebugAnalyticsProvider());
            AnalyticsManager.AddProvider(new FirebaseAnalyticsProvider());
            AnalyticsManager.AddProvider(new GameAnalyticsProvider());

            AnalyticsManager.Initialize();

            // Đồng bộ trạng thái âm thanh đã lưu — chạm vào Instance là AudioManager tự dựng và tự load setting.
            _ = AudioManager.Instance;
        }
    }
}

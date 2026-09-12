using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace HC.EditorTools
{
    /// <summary>
    /// Kiểm tra project đã cắm đủ thứ chưa và báo cáo ra Console.
    ///
    /// Dùng khi: vừa clone repo về máy mới, vừa import SDK xong, hoặc dữ liệu không về dashboard
    /// mà không hiểu vì sao. Mỗi dòng FAIL đều kèm luôn cách sửa — không phải đi tra tài liệu.
    /// </summary>
    public static class SetupChecker
    {
        private const string GoogleServicesPath = "Assets/google-services.json";
        private const string GameAnalyticsSettingsPath = "Assets/Resources/GameAnalytics/Settings.asset";

        private enum Level { Pass, Warn, Fail }

        private readonly struct Line
        {
            public readonly Level Level;
            public readonly string Title;
            public readonly string Detail;

            public Line(Level level, string title, string detail = null)
            {
                Level = level;
                Title = title;
                Detail = detail;
            }
        }

        [MenuItem("HC Template/Kiểm tra setup", priority = 10)]
        public static void Check()
        {
            var lines = new List<Line>
            {
                CheckFirebaseSdk(),
                CheckGoogleServices(),
                CheckPackageName(),
                CheckGameAnalyticsSdk(),
                CheckGameAnalyticsKeys(),
                CheckAndroidModule()
            };

            var sb = new StringBuilder();
            sb.AppendLine("=== HC Template · kiểm tra setup ===");

            int fails = 0, warns = 0;

            foreach (var line in lines)
            {
                string mark = line.Level switch
                {
                    Level.Pass => "  OK  ",
                    Level.Warn => " CHÚ Ý",
                    _ => " THIẾU"
                };

                if (line.Level == Level.Fail) fails++;
                if (line.Level == Level.Warn) warns++;

                sb.Append('[').Append(mark).Append("] ").AppendLine(line.Title);
                if (!string.IsNullOrEmpty(line.Detail)) sb.Append("          → ").AppendLine(line.Detail);
            }

            sb.AppendLine();
            sb.Append(fails == 0
                ? (warns == 0 ? "Đủ hết. Chạy được rồi." : $"Chạy được, nhưng có {warns} mục cần để ý.")
                : $"Còn {fails} mục thiếu — sửa xong chạy lại lệnh này.");

            if (fails > 0) Debug.LogError(sb.ToString());
            else if (warns > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        private static Line CheckFirebaseSdk()
        {
            bool hasDefine = HasDefine("HC_FIREBASE");
            bool hasFolder = Directory.Exists("Assets/Firebase");

            if (hasDefine && hasFolder) return new Line(Level.Pass, "Firebase SDK đã import");

            if (hasFolder)
            {
                return new Line(Level.Warn, "Firebase SDK có thư mục nhưng define chưa bật",
                                "Chạy menu HC Template > Dò lại SDK define, hoặc đợi Unity compile xong.");
            }

            return new Line(Level.Fail, "Chưa import Firebase SDK",
                            "Tải firebase.google.com/download/unity → giải nén → import FirebaseAnalytics.unitypackage");
        }

        private static Line CheckGoogleServices()
        {
            if (File.Exists(GoogleServicesPath)) return new Line(Level.Pass, "google-services.json có trong Assets/");

            return new Line(Level.Fail, "Thiếu google-services.json",
                            "Firebase Console > Project settings > Your apps > tải về, bỏ vào Assets/. " +
                            "File này KHÔNG nằm trong Git nên máy mới phải tự lấy.");
        }

        private static Line CheckPackageName()
        {
            string unityId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);

            if (!File.Exists(GoogleServicesPath))
            {
                return new Line(Level.Warn, "Chưa đối chiếu được package name",
                                $"Unity đang để: {unityId} — chưa có google-services.json để so.");
            }

            string json = File.ReadAllText(GoogleServicesPath);
            var match = Regex.Match(json, "\"package_name\"\\s*:\\s*\"([^\"]+)\"");

            if (!match.Success)
            {
                return new Line(Level.Warn, "google-services.json không có package_name",
                                "File có thể hỏng — tải lại từ Firebase Console.");
            }

            string firebaseId = match.Groups[1].Value;

            if (unityId == firebaseId)
            {
                return new Line(Level.Pass, $"Package name khớp Firebase ({unityId})");
            }

            return new Line(Level.Fail, "Package name KHÔNG khớp Firebase",
                            $"Unity: {unityId} · Firebase: {firebaseId}. " +
                            "Lệch là event bay vào hư không, không báo lỗi gì cả. Sửa ở " +
                            "Project Settings > Player > Android > Other Settings > Package Name.");
        }

        private static Line CheckGameAnalyticsSdk()
        {
            bool hasDefine = HasDefine("HC_GAMEANALYTICS");
            bool hasFolder = Directory.Exists("Assets/GameAnalytics");

            if (hasDefine && hasFolder) return new Line(Level.Pass, "GameAnalytics SDK đã import");

            if (hasFolder)
            {
                return new Line(Level.Warn, "GameAnalytics SDK có thư mục nhưng define chưa bật",
                                "Chạy menu HC Template > Dò lại SDK define.");
            }

            return new Line(Level.Fail, "Chưa import GameAnalytics SDK",
                            "github.com/GameAnalytics/GA-SDK-UNITY/releases → tải GA_SDK_UNITY.unitypackage");
        }

        private static Line CheckGameAnalyticsKeys()
        {
            if (!File.Exists(GameAnalyticsSettingsPath))
            {
                return new Line(Level.Fail, "Chưa có file cài đặt GameAnalytics",
                                "Mở Window > GameAnalytics > Select Settings một lần để Unity tạo file.");
            }

            string asset = File.ReadAllText(GameAnalyticsSettingsPath);
            bool hasGameKey = Regex.IsMatch(asset, @"gameKey:\s*\r?\n\s*-\s*\S");
            bool hasSecret = Regex.IsMatch(asset, @"secretKey:\s*\r?\n\s*-\s*\S");

            if (hasGameKey && hasSecret) return new Line(Level.Pass, "GameAnalytics đã có Game Key và Secret Key");

            return new Line(Level.Fail, "GameAnalytics chưa điền key",
                            "Window > GameAnalytics > Select Settings → đăng nhập rồi chọn Studio/Game, " +
                            "hoặc dán tay Game Key + Secret Key. Nhớ File > Save Project sau khi dán.");
        }

        private static Line CheckAndroidModule()
        {
            bool supported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);

            if (supported) return new Line(Level.Pass, "Android Build Support đã cài");

            return new Line(Level.Warn, "Chưa cài Android Build Support",
                            "Unity Hub > Installs > bánh răng > Add modules. Không có nó thì không build được " +
                            "máy thật, mà Firebase Analytics chỉ chạy trên máy thật.");
        }

        private static bool HasDefine(string symbol)
        {
            string raw = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            foreach (var define in raw.Split(';'))
            {
                if (define.Trim() == symbol) return true;
            }

            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace HC.EditorTools
{
    /// <summary>
    /// Tự bật/tắt scripting define theo việc SDK bên thứ ba có mặt trong project hay không.
    ///
    /// Vì sao cần: SDK không nằm trong Git (xem .gitignore — Firebase nặng 229MB, có file vượt giới hạn
    /// 100MB của GitHub). Clone repo về máy mới là KHÔNG có SDK. Nếu provider gọi thẳng Firebase thì
    /// project không compile nổi, kẹt cứng ngay bước đầu.
    ///
    /// Cách giải: code gọi SDK nằm sau <c>#if HC_FIREBASE</c> / <c>#if HC_GAMEANALYTICS</c>. File này
    /// dò xem SDK có thật không rồi bật define tương ứng. Không có SDK thì project vẫn build, provider
    /// chỉ đứng yên và in cảnh báo. Import SDK vào là define tự bật, không phải làm gì thêm.
    /// </summary>
    [InitializeOnLoad]
    public static class SdkDefines
    {
        private const string FirebaseDefine = "HC_FIREBASE";
        private const string GameAnalyticsDefine = "HC_GAMEANALYTICS";

        private const string FirebaseProbeType = "Firebase.Analytics.FirebaseAnalytics";
        private const string GameAnalyticsProbeType = "GameAnalyticsSDK.GameAnalytics";

        private static readonly NamedBuildTarget[] Targets =
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS
        };

        static SdkDefines()
        {
            // Chạy trễ một nhịp: lúc constructor này chạy, Unity có thể chưa nạp xong mọi assembly.
            EditorApplication.delayCall += Sync;
        }

        [MenuItem("HC Template/Dò lại SDK define", priority = 20)]
        public static void Sync()
        {
            bool hasFirebase = TypeExists(FirebaseProbeType);
            bool hasGameAnalytics = TypeExists(GameAnalyticsProbeType);

            var changes = new List<string>();

            foreach (var target in Targets)
            {
                string raw = PlayerSettings.GetScriptingDefineSymbols(target);
                var defines = raw.Split(';')
                                 .Select(d => d.Trim())
                                 .Where(d => d.Length > 0)
                                 .ToList();

                bool changed = Apply(defines, FirebaseDefine, hasFirebase);
                changed |= Apply(defines, GameAnalyticsDefine, hasGameAnalytics);

                if (!changed) continue;

                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
                changes.Add(target.TargetName);
            }

            if (changes.Count == 0) return;

            Debug.Log($"[SdkDefines] Cập nhật define cho {string.Join(", ", changes)} — " +
                      $"Firebase: {(hasFirebase ? "có" : "không")}, " +
                      $"GameAnalytics: {(hasGameAnalytics ? "có" : "không")}. Unity sẽ compile lại.");
        }

        /// <returns>true nếu danh sách define bị thay đổi.</returns>
        private static bool Apply(List<string> defines, string symbol, bool shouldExist)
        {
            bool exists = defines.Contains(symbol);

            if (shouldExist && !exists)
            {
                defines.Add(symbol);
                return true;
            }

            if (!shouldExist && exists)
            {
                defines.Remove(symbol);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Dò theo TÊN KIỂU chứ không theo đường dẫn thư mục: thư mục có thể còn sót lại sau khi gỡ SDK,
        /// còn kiểu thì chỉ tồn tại khi SDK thật sự compile được.
        /// </summary>
        private static bool TypeExists(string fullTypeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType(fullTypeName, false) != null) return true;
                }
                catch (Exception)
                {
                    // Vài assembly động không cho gọi GetType — bỏ qua, không phải lỗi.
                }
            }

            return false;
        }
    }
}

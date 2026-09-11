using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace HC.Core
{
    /// <summary>
    /// Lưu/đọc <see cref="SaveData"/> ra file JSON trong Application.persistentDataPath.
    /// Static — không cần gắn vào GameObject nào, gọi được từ bất kỳ đâu, kể cả trước khi scene load xong.
    ///
    /// Ghi theo kiểu atomic (ghi file tạm rồi mới thay thế) + giữ 1 bản .bak, nên tắt máy hay hết pin
    /// giữa lúc ghi cũng không mất sạch tiến trình của người chơi.
    /// </summary>
    public static class SaveManager
    {
        private const string FileName = "save.json";
        private const string TempExtension = ".tmp";
        private const string BackupExtension = ".bak";

        private static SaveData _cached;

        /// <summary>Bắn ra sau mỗi lần load thành công (kể cả lần tạo save mới).</summary>
        public static event Action<SaveData> OnLoaded;

        /// <summary>Bắn ra sau mỗi lần ghi file thành công.</summary>
        public static event Action<SaveData> OnSaved;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        private static string BackupPath => FilePath + BackupExtension;

        private static string TempPath => FilePath + TempExtension;

        public static bool HasSave => File.Exists(FilePath) || File.Exists(BackupPath);

        /// <summary>
        /// Bản save đang dùng trong phiên chơi này. Lần gọi đầu tự đọc từ file.
        /// Sửa xong nhớ gọi <see cref="Save()"/>.
        /// </summary>
        public static SaveData Data => _cached ??= Load();

        // Enter Play Mode không reload domain => static giữ lại data của lần chạy trước. Dòng này lo việc đó.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _cached = null;
            OnLoaded = null;
            OnSaved = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void HookAutoSave()
        {
            // Trên mobile, OnApplicationQuit thường KHÔNG chạy: OS kill thẳng app khi thiếu RAM.
            // Mốc đáng tin duy nhất là lúc app mất focus (bấm Home / chuyển app).
            Application.focusChanged -= OnFocusChanged;
            Application.focusChanged += OnFocusChanged;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus && _cached != null) Save(_cached);
        }

        private static void OnQuitting()
        {
            if (_cached != null) Save(_cached);
        }

        /// <summary>
        /// Đọc save từ file. File hỏng thì thử bản .bak, hỏng nốt thì trả về SaveData mặc định
        /// (không bao giờ trả null, và không bao giờ ném exception ra ngoài).
        /// </summary>
        public static SaveData Load()
        {
            var data = ReadFrom(FilePath);

            if (data == null && File.Exists(BackupPath))
            {
                Debug.LogWarning("[SaveManager] File save chính hỏng — đang khôi phục từ bản .bak.");
                data = ReadFrom(BackupPath);
            }

            if (data == null)
            {
                if (HasSave) Debug.LogError("[SaveManager] Không đọc được save nào — tạo mới. Tiến trình cũ đã mất.");
                data = new SaveData();
            }

            Migrate(data);

            _cached = data;
            OnLoaded?.Invoke(data);
            return data;
        }

        /// <summary>Ghi <see cref="Data"/> hiện tại xuống file.</summary>
        public static void Save()
        {
            Save(Data);
        }

        /// <summary>Ghi data xuống file và đặt nó làm bản đang dùng.</summary>
        public static void Save(SaveData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[SaveManager] Save(null) — bỏ qua.");
                return;
            }

            _cached = data;
            data.saveVersion = SaveData.CurrentVersion;

            // prettyPrint chỉ bật ở development build: file to hơn ~30% nhưng mở ra đọc/sửa tay được lúc debug.
            string json = JsonUtility.ToJson(data, Debug.isDebugBuild);

            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);

                // Ghi ra file tạm trước. Nếu chết máy ở bước này thì save cũ vẫn còn nguyên.
                File.WriteAllText(TempPath, json, Encoding.UTF8);

                if (File.Exists(FilePath))
                {
                    // File.Replace làm nguyên tử + tự đẩy bản cũ sang .bak.
                    File.Replace(TempPath, FilePath, BackupPath);
                }
                else
                {
                    File.Move(TempPath, FilePath);
                }

                OnSaved?.Invoke(data);
            }
            catch (Exception e)
            {
                // File.Replace không được hỗ trợ đều trên mọi filesystem Android — fallback copy thủ công.
                Debug.LogWarning($"[SaveManager] Ghi kiểu atomic thất bại ({e.GetType().Name}: {e.Message}) — thử cách thường.");
                try
                {
                    if (File.Exists(FilePath)) File.Copy(FilePath, BackupPath, true);
                    File.WriteAllText(FilePath, json, Encoding.UTF8);
                    if (File.Exists(TempPath)) File.Delete(TempPath);
                    OnSaved?.Invoke(data);
                }
                catch (Exception e2)
                {
                    Debug.LogError($"[SaveManager] Không ghi được save: {e2}");
                }
            }
        }

        /// <summary>Xoá sạch save (nút "Reset progress" trong settings, hoặc lúc test).</summary>
        public static void Delete()
        {
            TryDelete(FilePath);
            TryDelete(BackupPath);
            TryDelete(TempPath);
            _cached = null;
        }

        private static SaveData ReadFrom(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return null;

                // JsonUtility trả null khi JSON hỏng, và ném exception khi sai cú pháp nặng — chặn cả hai.
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Đọc \"{path}\" thất bại: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Nâng cấp save cũ lên cấu trúc hiện tại. Mỗi lần đổi <see cref="SaveData"/> theo kiểu
        /// không tương thích ngược thì tăng SaveData.CurrentVersion và thêm 1 nhánh ở đây.
        /// </summary>
        private static void Migrate(SaveData data)
        {
            if (data.saveVersion == SaveData.CurrentVersion) return;

            if (data.saveVersion > SaveData.CurrentVersion)
            {
                // Người chơi vừa hạ cấp app (hiếm, nhưng có qua TestFlight/APK tay).
                Debug.LogWarning($"[SaveManager] Save version {data.saveVersion} mới hơn app ({SaveData.CurrentVersion}) — dùng nguyên trạng.");
                return;
            }

            // Ví dụ khi lên version 2:
            // if (data.saveVersion < 2) { data.soundOn = true; }

            Debug.Log($"[SaveManager] Đã nâng save từ version {data.saveVersion} lên {SaveData.CurrentVersion}.");
            data.saveVersion = SaveData.CurrentVersion;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveManager] Không xoá được \"{path}\": {e.Message}");
            }
        }
    }
}

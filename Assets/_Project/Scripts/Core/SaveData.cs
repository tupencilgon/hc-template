using System;

namespace HC.Core
{
    /// <summary>
    /// Toàn bộ dữ liệu lưu của người chơi. Đây là file **được phép sửa ở mỗi game** —
    /// thêm field thoải mái, <see cref="SaveManager"/> không cần đụng tới.
    ///
    /// Ràng buộc của JsonUtility (nhớ kỹ, sai là mất data mà không báo lỗi):
    /// - Chỉ serialize field public hoặc field có [SerializeField]. Property (có get/set) bị bỏ qua.
    /// - Không hỗ trợ Dictionary. Cần map thì dùng 2 List song song hoặc List các struct [Serializable].
    /// - Không hỗ trợ kiểu nullable (int?), interface, hay class trừu tượng.
    /// - Field mới thêm vào bản cập nhật sẽ nhận giá trị mặc định của C# (0/false/null), KHÔNG phải
    ///   giá trị khởi tạo ghi ở đây — vì JsonUtility ghi đè lên object đã tạo sẵn.
    ///   Muốn giá trị mặc định khác 0 cho save cũ thì xử lý trong SaveManager.Migrate().
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Tăng số này mỗi lần đổi cấu trúc dữ liệu, rồi viết bước chuyển trong SaveManager.Migrate().</summary>
        public const int CurrentVersion = 1;

        public int saveVersion = CurrentVersion;

        public int coins;
        public int highScore;
        public bool soundOn = true;
    }
}

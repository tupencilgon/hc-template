# Setup trên máy mới (hoặc sau khi clone cho game mới)

SDK bên thứ ba **không nằm trong Git** — Firebase nặng 229 MB và có file 107 MB, vượt giới hạn
100 MB/file của GitHub. Repo giữ ở mức ~500 KB, đổi lại phải import SDK lại một lần.

Project **vẫn build được ngay cả khi chưa có SDK** — code gọi SDK nằm sau `#if HC_FIREBASE` /
`#if HC_GAMEANALYTICS`, và `SdkDefines.cs` tự bật define khi phát hiện SDK. Không có SDK thì provider
chỉ đứng yên và in cảnh báo, không chặn ai làm việc.

---

## Cách nhanh nhất: để Unity tự nói thiếu gì

```
Menu  HC Template → Kiểm tra setup
```

In ra Console 6 dòng trạng thái, mỗi dòng thiếu đều kèm cách sửa. Chạy lại sau mỗi lần sửa.

---

## Danh sách đầy đủ

### 1. Firebase Analytics — phiên bản đang dùng: **13.16.0**

| Việc | Chi tiết |
|---|---|
| Tải SDK | <https://firebase.google.com/download/unity> — file zip, một bộ dùng chung Android + iOS |
| Import | Giải nén → `Assets → Import Package → Custom Package…` → **chỉ** `FirebaseAnalytics.unitypackage` |
| File cấu hình | Firebase Console → Project settings → Your apps → tải `google-services.json` → bỏ vào `Assets/` |
| Package name | `Project Settings → Player → Android → Other Settings → Package Name` phải **khớp y hệt** `package_name` trong `google-services.json` |

> `google-services.json` bị `.gitignore` chặn nên clone về sẽ không có. Cất một bản ở Google Drive.
> Lệch package name là event bay vào hư không, **không báo lỗi gì cả** — menu *Kiểm tra setup* bắt được lỗi này.

### 2. GameAnalytics — phiên bản đang dùng: **8.2.0**

| Việc | Chi tiết |
|---|---|
| Tải SDK | <https://github.com/GameAnalytics/GA-SDK-UNITY/releases> → bấm **Assets** → `GA_SDK_UNITY.unitypackage` (~12 MB) |
| Bỏ qua | `GA_ILRD_UNITY.unitypackage` — gói theo dõi doanh thu quảng cáo, chỉ cần khi đã cắm ads |
| Import | `Assets → Import Package → Custom Package…` |
| Điền key | `Window → GameAnalytics → Select Settings` → đăng nhập chọn Studio/Game, hoặc dán tay Game Key + Secret Key |
| Lưu lại | **`File → Save Project`** — không lưu thì key chưa ghi xuống đĩa |

> Key nằm trong `Assets/Resources/GameAnalytics/Settings.asset`, đã bị `.gitignore` chặn vì repo công khai.
> Mỗi game có Game Key riêng, không dùng lại của template.

### 3. Android Build Support

Unity Hub → Installs → bánh răng → **Add modules** → Android Build Support.

Firebase Analytics **không chạy trong Unity Editor** — chỉ hoạt động trên máy Android thật hoặc máy ảo.
Trong Editor chỉ có `DebugAnalyticsProvider` in ra Console, và như vậy là đúng, không phải lỗi.

---

## Khi clone template cho một game MỚI

Ngoài 3 mục trên, phải đổi toàn bộ bộ định danh — dùng lại của template là số liệu hai game trộn vào nhau:

- [ ] Package name mới: `com.tastudio.<tengame>`
- [ ] App Android + iOS mới trong Firebase (project mới hoặc thêm app vào project cũ) → `google-services.json` mới
- [ ] Game mới trên GameAnalytics → Game Key + Secret Key mới (nhớ: **mỗi nền tảng một Game riêng**)
- [ ] `Product Name` trong Player Settings
- [ ] Xoá `Assets/_Project/Scripts/Demo/`, `Assets/_Project/Scripts/Editor/DemoSceneBuilder.cs`, scene `Demo_Core`
- [ ] Viết `Docs/concept-doc.md` trước khi code bất cứ thứ gì

---

## Xác minh dữ liệu thật sự về

Chưa nhìn tận mắt event chạy về thì coi như chưa xong.

**Firebase** — bật DebugView (cần điện thoại cắm dây và công cụ `adb`):

```bash
adb shell setprop debug.firebase.analytics.app com.tastudio.hctemplate
```

Mở game trên máy → Firebase Console → Analytics → **DebugView** → thấy `level_start` nhảy lên là xong.

**GameAnalytics** — dashboard có mục realtime, event hiện trong vài phút. Trong Unity Console cũng thấy
dòng `[Analytics/GameAnalytics] Sẵn sàng.` khi SDK init xong.

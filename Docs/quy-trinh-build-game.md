# Quy trình chuẩn build game — sổ tay vận hành

Tài liệu này là phần "cách làm". Phần "làm gì, theo thứ tự nào" nằm ở pipeline gốc (5 giai đoạn).
Mỗi game clone từ `hc-template` đều mang theo file này.

---

## 1. Từ điển thuật ngữ

### Chỉ số đánh giá game (publisher nhìn vào đây để quyết định scale hay bỏ)

| Thuật ngữ | Nghĩa | Ghi chú thực dụng |
|---|---|---|
| **CPI** — Cost Per Install | Tốn bao nhiêu tiền quảng cáo để có 1 lượt cài | Càng thấp càng tốt. Đây là chỉ số **số một** ở vòng test đầu |
| **IPM** — Installs Per Mille | Số lượt cài trên 1000 lượt xem quảng cáo | Đo sức hút của creative. IPM cao → CPI thấp |
| **CTR** — Click Through Rate | Tỉ lệ người xem quảng cáo rồi bấm vào | |
| **D1 / D7 / D30 Retention** | % người chơi quay lại sau 1 / 7 / 30 ngày | D1 là cửa ải đầu. Dưới mốc là game chết, không cứu |
| **Session length** | Thời lượng trung bình 1 lượt chơi | |
| **Playtime** | Tổng thời gian chơi mỗi ngày của 1 người | |
| **LTV** — Lifetime Value | Tổng tiền 1 người chơi mang lại trong cả đời dùng app | |
| **ROAS** — Return On Ad Spend | Doanh thu chia chi phí quảng cáo | ROAS > 1 nghĩa là có lãi |
| **ARPDAU** | Doanh thu trung bình trên 1 người chơi hoạt động mỗi ngày | Chỉ số theo dõi hằng ngày khi game đã live |
| **DAU / MAU** | Số người chơi hoạt động mỗi ngày / mỗi tháng | |

### Quảng cáo

| Thuật ngữ | Nghĩa |
|---|---|
| **Ad network** | Nơi bán quảng cáo (AdMob, Unity Ads, Meta Audience Network, ironSource...) |
| **Mediation** | Lớp trung gian đứng trên nhiều network, mỗi lần cần hiển thị thì cho các network "đấu giá" chọn bên trả cao nhất. **AppLovin MAX là một mediation** |
| **Waterfall** | Kiểu mediation cũ: gọi lần lượt từng network theo thứ tự giá đã định sẵn |
| **In-app bidding** | Kiểu mới: mọi network đấu giá đồng thời theo thời gian thực. Ăn tiền hơn waterfall |
| **eCPM** | Doanh thu trên 1000 lượt hiển thị quảng cáo. Đây là "giá" của mỗi ngàn lượt xem |
| **Fill rate** | % số lần xin quảng cáo mà thực sự có quảng cáo trả về |
| **Impression** | 1 lượt hiển thị quảng cáo |
| **Interstitial** | Quảng cáo toàn màn hình chen giữa các màn chơi |
| **Rewarded video** | Người chơi tự nguyện xem để đổi phần thưởng. Loại này eCPM cao nhất và không phá UX |
| **Banner** | Dải quảng cáo dính mép màn hình |
| **Ad Unit ID** | Mã định danh 1 vị trí quảng cáo. Mỗi loại quảng cáo, mỗi nền tảng là 1 mã riêng |
| **Placement** | Tên vị trí trong game theo cách gọi của mình, vd `level_end`, `revive` |

### Kỹ thuật

| Thuật ngữ | Nghĩa |
|---|---|
| **SDK** | Bộ thư viện của bên thứ ba cắm vào game (Firebase SDK, AppLovin SDK...) |
| **Wrapper / Facade** | Lớp code của mình bọc ngoài SDK. Gameplay chỉ nói chuyện với lớp này |
| **Provider** | Một bản cài đặt cụ thể phía sau facade (FirebaseProvider, GameAnalyticsProvider...) |
| **Event** | Một hành động được ghi nhận, vd `level_complete` |
| **Parameter** | Dữ liệu kèm theo event, vd `level_index = 7` |
| **User property** | Thuộc tính gắn với người chơi chứ không gắn với event, vd `player_level` |
| **Funnel** | Phễu: xem người chơi rụng ở bước nào trong chuỗi hành động |
| **Bundle ID / Package name** | Mã định danh app, dạng `com.congty.tengame`. **Đặt xong gần như không đổi được** |
| **GAID / IDFA** | Mã quảng cáo của thiết bị Android / iOS |
| **ATT** | Hộp thoại iOS xin phép theo dõi. Từ chối thì mất dữ liệu quảng cáo |
| **GDPR / CCPA** | Luật bảo vệ dữ liệu ở EU / California. Phải có màn hình xin đồng ý |
| **`google-services.json`** | File cấu hình Firebase cho Android. **Không commit lên Git** — đã chặn sẵn trong `.gitignore` |

### Phát hành

| Thuật ngữ | Nghĩa |
|---|---|
| **UA** — User Acquisition | Việc mua lượt cài bằng tiền quảng cáo |
| **Creative** | Video/ảnh dùng làm quảng cáo cho game |
| **Soft launch** | Phát hành thử ở vài nước nhỏ để đo số trước khi bung rộng |
| **Store listing** | Trang giới thiệu app trên store |
| **Internal / Closed / Open testing** | Ba mức thử nghiệm trên Google Play, độ mở tăng dần |

---

## 2. Vì sao template có lớp wrapper

Nguyên tắc: **code gameplay không bao giờ gọi thẳng SDK.**

```
Gameplay  →  AnalyticsManager  →  IAnalyticsProvider  →  Firebase SDK
                  (của mình)          (của mình)           (bên thứ ba)
```

Bốn cái lợi, theo thứ tự quan trọng:

1. **Gắn event được ngay hôm nay**, kể cả khi chưa có SDK nào trong máy. Provider `Debug` in ra Console là đủ để code và test.
2. **Đổi nhà cung cấp không phải sửa gameplay.** Publisher bắt đổi từ GameAnalytics sang thứ khác → viết provider mới, gameplay giữ nguyên.
3. **Chuẩn hoá tên event giữa mọi game.** Số liệu game này so sánh được với game kia.
4. **Chặn lỗi tại một chỗ.** Ví dụ Firebase âm thầm bỏ event có tên sai định dạng — wrapper kiểm tra và báo lỗi ngay lúc dev, thay vì một tháng sau mới phát hiện không có số liệu.

Lớp Ads sau này cũng theo đúng khuôn đó: `AdManager` → `IAdProvider` → AppLovin MAX. **Đây chính là cách gác AppLovin mà không kẹt tiến độ** — gameplay gọi `AdManager.ShowInterstitial()` từ bây giờ, phía sau tạm là provider rỗng luôn trả về "xong".

---

## 3. Quy trình thêm 1 hệ thống vào template

Vòng lặp đã chạy cho cả 4 hệ thống Core, giữ nguyên cho các hệ thống sau:

1. **Chốt phạm vi** — hệ thống này làm gì, KHÔNG làm gì
2. **Claude Code viết** vào đúng thư mục theo cấu trúc chuẩn
3. **Compile check bằng Roslyn của Unity** — không cần mở Editor, xem `README.md`
4. **Click vào Unity Editor** để sinh file `.meta`
5. **Chạy thử thật** trong scene demo — compile được không có nghĩa là chạy đúng
6. **Commit cả `.cs` lẫn `.cs.meta`**, push
7. **Tick vào checklist** trong `README.md` + ghi `Docs/changelog.md`

Hai luật cứng:
- **Không commit code chưa compile.**
- **Không commit thiếu `.meta`.** File `.meta` giữ GUID; thiếu nó thì máy khác clone về là prefab/scene mất hết reference.

---

## 4. Quy trình tích hợp Analytics

### Khâu 1 — Lớp wrapper ✅ đã xong
`Assets/_Project/Scripts/Analytics/` — `AnalyticsManager`, `IAnalyticsProvider`, `DebugAnalyticsProvider`.
Gắn event vào gameplay được ngay, chưa cần SDK.

### Khâu 2 — Đăng ký tài khoản (không cần app live)

**Firebase Analytics** — miễn phí, không giới hạn số event.
1. `console.firebase.google.com` → tạo project
2. Thêm app Android → nhập **package name**, phải khớp y hệt `Player Settings > Other Settings > Package Name` trong Unity
3. Tải `google-services.json` → bỏ vào thư mục `Assets/`
4. Tải Firebase Unity SDK → import **riêng** `FirebaseAnalytics.unitypackage` (đừng import cả bộ, nặng vô ích)

**GameAnalytics** — miễn phí, mạnh ở phễu theo màn chơi, nhiều publisher quen dùng.
1. `gameanalytics.com` → tạo Studio → tạo Game
2. Lấy **Game Key** và **Secret Key** (Android và iOS là 2 bộ khác nhau)
3. Import GameAnalytics Unity SDK

> **Package name chọn một lần dùng mãi.** Đặt trước khi làm gì khác, theo dạng `com.<tên>.<tengame>`.

### Khâu 3 — Viết provider
Mỗi SDK một file cài đặt `IAnalyticsProvider`, đăng ký ở bootstrap:

```csharp
AnalyticsManager.AddProvider(new FirebaseAnalyticsProvider());
AnalyticsManager.AddProvider(new GameAnalyticsProvider());
AnalyticsManager.Initialize();
```

### Khâu 4 — Gắn event vào gameplay
Đúng 5 event chuẩn, không tự chế thêm ở giai đoạn gray-box:
`level_start` · `level_complete` · `level_fail` · `ad_shown` · `ad_reward_claimed`

### Khâu 5 — Xác minh có số liệu thật
Bật **DebugView** của Firebase để xem event chạy về theo thời gian thực. Không nhìn tận mắt event về tới server thì coi như chưa tích hợp xong.

---

## 5. Quy trình tích hợp Ads (để dành)

Thứ tự bắt buộc, không đảo được:

1. Có tài khoản mediation đã được duyệt (AppLovin MAX)
2. Tạo Ad Unit ID cho từng loại × từng nền tảng
3. Import SDK, khai báo Ad Unit ID
4. Viết `AdManager` + provider
5. Test bằng quảng cáo thử (**test ad**) — bấm vào quảng cáo thật trên máy mình là bị khoá tài khoản vì gian lận nhấp chuột

**Vì sao bước 1 hay kẹt:** mạng quảng cáo cần xác minh mình là nhà phát hành thật, thường bằng một app có trên store. Con gà quả trứng — cần app live để có tài khoản, cần tài khoản để cắm quảng cáo vào app.

Cách gỡ: **làm ngược thứ tự.** Đưa game đầu tiên lên Google Play **chưa cắm quảng cáo** để có store listing trước, rồi mới xin duyệt tài khoản mediation, rồi mới cắm quảng cáo vào bản cập nhật. Nhờ có lớp `AdManager`, bước cắm này chỉ là thêm một provider.

Hai thứ cần tự kiểm chứng trước khi lên lịch, vì chính sách store đổi liên tục:
- Track nào của Google Play thì cho ra **URL công khai truy cập được** — link internal testing là dạng opt-in, có thể không được chấp nhận làm bằng chứng xác minh. Open testing mở hơn.
- Tài khoản developer cá nhân mở gần đây có thể bị yêu cầu **chạy closed testing với một số lượng tester tối thiểu trong một số ngày tối thiểu** trước khi được phát hành production. Kiểm tra sớm vì nó ảnh hưởng thẳng tới lịch ra game.

---

## 6. Checklist trước khi gửi publisher

- [ ] 5 event chuẩn đã bắn và đã **nhìn thấy** trên dashboard
- [ ] Không còn `Debug.Log` rác trong vòng lặp gameplay
- [ ] Build chạy được trên máy Android thật, không chỉ trong Editor
- [ ] FPS ổn định trên máy tầm trung
- [ ] Không commit key thật (`google-services.json`, keystore) — kiểm tra lại `.gitignore`
- [ ] `Docs/concept-doc.md` viết xong, đủ để publisher hiểu game trong 1 phút
- [ ] `Docs/test-results.md` có số liệu tự test

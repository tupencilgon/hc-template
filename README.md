# hc-template

Unity boilerplate dùng chung cho mọi game hybrid-casual. **Mọi game mới clone từ repo này**, không code lại từ đầu.

- Unity: `6000.3.23f1` (URP)
- Naming convention game mới: `hc-<genre>-<tên-ngắn>` (vd: `hc-arcade-noodle-run`)

## Cấu trúc thư mục

```
Assets/
├── _Project/          # code + asset của mình (tách khỏi 3rd-party)
│   ├── Scripts/
│   │   ├── Core/      # bootstrap, scene loader, save/load, pooling, audio
│   │   ├── Gameplay/  # logic riêng từng game
│   │   ├── UI/        # menu, HUD, pause/settings
│   │   ├── Ads/       # wrapper AppLovin MAX
│   │   └── Analytics/ # wrapper Firebase + GameAnalytics
│   ├── Prefabs/
│   ├── Scenes/
│   └── Art/
│       ├── Models/    # xuất từ Astra
│       ├── Textures/
│       └── UI/        # xuất từ GPT
└── Plugins/           # SDK bên thứ 3
Docs/
├── concept-doc.md     # nguồn sự thật duy nhất của mỗi game
├── test-results.md    # KPI test + checklist go/no-go
└── changelog.md
```

## Checklist boilerplate

- [ ] Ad mediation (AppLovin MAX): interstitial / rewarded / banner wire sẵn, chỉ đổi Ad Unit ID
- [ ] Analytics wrapper (Firebase + GameAnalytics): `level_start`, `level_complete`, `level_fail`, `ad_shown`, `ad_reward_claimed`
- [x] Object pooling system (`Scripts/Core/ObjectPool.cs`)
- [x] Audio Manager (`Scripts/Core/AudioManager.cs`)
- [ ] Scene loader + loading screen
- [ ] Settings / Pause menu skeleton (chốt 1 chuẩn UI: uGUI hoặc UI Toolkit, dùng xuyên suốt)
- [ ] Save/load system (JSON, chưa cần cloud)
- [ ] IAP hook (Unity IAP) — cắm sẵn nhưng **tắt**
- [ ] Debug overlay: FPS + draw call counter
- [x] `.gitignore` chuẩn Unity + folder structure

## Object Pool

Namespace: `HC.Core`. Dùng `Get()` thay `Instantiate`, `Return()` thay `Destroy`.

```csharp
using HC.Core;

// Khai báo pool: kéo prefab vào list Configs trên component [ObjectPool] trong scene,
// hoặc đăng ký lúc runtime:
ObjectPool.Instance.RegisterPool("enemy", enemyPrefab, prewarmCount: 20, maxSize: 50);

// Spawn
var enemy = ObjectPool.Instance.Get("enemy", spawnPoint.position, Quaternion.identity);

// Trả về pool — thay cho Destroy(gameObject)
ObjectPool.Instance.Return(enemy);
ObjectPool.Instance.Return(vfx, 1.5f);   // trả sau 1.5s, dùng cho VFX/đạn
ObjectPool.Instance.ReturnAll();         // dọn sạch lúc restart level
```

Prefab cần reset state mỗi lần spawn thì implement `IPoolable`:

```csharp
public class Bullet : MonoBehaviour, IPoolable
{
    public void OnSpawnFromPool() { _rb.linearVelocity = Vector3.zero; _trail.Clear(); }
    public void OnReturnToPool()  { }
}
```

Lưu ý:
- `Instance` tự tạo GameObject `[ObjectPool]` nếu scene chưa có, và `DontDestroyOnLoad` qua các scene
- `Return()` một object không thuộc pool nào → bị **Destroy** kèm warning (để `Return` luôn đúng nghĩa "dọn đi")
- `IPoolable` được cache lúc tạo instance → 0 byte GC mỗi lần spawn, nhưng component thêm lúc runtime sẽ không được gọi callback
- `maxSize > 0` thì pool đầy sẽ Destroy bớt thay vì phình bộ nhớ vô hạn

## Audio Manager

Namespace: `HC.Core`. 2 AudioSource tách biệt — BGM (loop) và SFX (one-shot chồng nhau được).

```csharp
using HC.Core;

AudioManager.Instance.PlayBGM(levelMusic);          // fade mặc định 0.5s, gọi lại cùng clip thì không restart
AudioManager.Instance.PlaySFX(coinClip);
AudioManager.Instance.PlaySFX(explosionClip, 0.6f); // nhỏ tiếng hơn

bool muted = AudioManager.Instance.ToggleMute();    // nút loa trên HUD, tự lưu PlayerPrefs
AudioManager.Instance.MusicVolume = 0.4f;           // slider trong Settings
AudioManager.Instance.ToggleSfx();                  // mute riêng SFX
```

UI settings đồng bộ lại khi state đổi:

```csharp
void OnEnable()  => AudioManager.Instance.OnAudioSettingsChanged += Refresh;
void OnDisable() { if (AudioManager.Instance != null) AudioManager.Instance.OnAudioSettingsChanged -= Refresh; }
```

Lưu ý:
- Mute/volume lưu PlayerPrefs (`audio.*`), tự load lại lúc mở game
- Có mute tổng (`ToggleMute`) và mute riêng nhạc/SFX — settings menu thường cần cả hai
- Cùng 1 clip gọi dồn trong 40ms bị bỏ qua (`sfxRetriggerCooldown`), tránh 20 đồng xu cùng frame làm vỡ tiếng
- Fade BGM chạy bằng `unscaledDeltaTime` nên vẫn mượt khi pause menu set `Time.timeScale = 0`

## Bắt đầu game mới

1. Clone repo này → đổi tên thành `hc-<genre>-<tên-ngắn>`
2. Đổi `Product Name` / `Bundle ID` trong Project Settings
3. Viết `Docs/concept-doc.md` trước khi code bất cứ thứ gì
4. Gray-box trong `Assets/_Project/Scripts/Gameplay/` — chưa dùng art thật

## Quy ước

- Không commit key/ID thật (Ad Unit ID production, keystore, `google-services.json`) — đã chặn trong `.gitignore`
- Mood board / reference ảnh nặng để trên Google Drive, chỉ link trong `concept-doc.md`
- Asset 3rd-party luôn nằm ngoài `_Project/` để dễ update/xoá

# Changelog

Format: [Keep a Changelog](https://keepachangelog.com/) · Version: `SemVer`

## [Unreleased]
### Added
- Cấu trúc thư mục chuẩn + `.gitignore` Unity + template `Docs/`
- Git LFS cho `*.fbx`, `*.png`, `*.psd`
- `Core/ObjectPool.cs` — object pool đa prefab theo string key, singleton, có prewarm/maxSize/IPoolable
- `Core/AudioManager.cs` — BGM/SFX tách 2 AudioSource, mute + volume lưu PlayerPrefs, fade BGM
- `Core/SaveManager.cs` + `Core/SaveData.cs` — save JSON atomic có .bak, autosave lúc mất focus, hook migrate theo version
- `Core/SceneLoader.cs` — LoadSceneAsync + loading screen dựng bằng code, fade, min duration, reset timeScale
- `UI/PauseMenu.cs` — pause/settings dựng bằng code, Esc + nút Back, tự pause khi bấm Home
- `Core/DebugOverlay.cs` — FPS/draw call qua ProfilerRecorder, chạy được trên máy thật
- `Analytics/FirebaseAnalyticsProvider.cs` + `GameAnalyticsProvider.cs` — cắm SDK thật, guard bằng `#if`
- `Editor/SdkDefines.cs` — tự bật/tắt define theo SDK có mặt hay không, để clone về máy trống vẫn build
- `Editor/SetupChecker.cs` — menu `HC Template > Kiểm tra setup`, soi package name/key/SDK
- `Core/GameBootstrap.cs` — tự đăng ký provider lúc mở game
- `Docs/setup-may-moi.md` — hướng dẫn dựng lại máy mới
- `Analytics/` — AnalyticsManager + IAnalyticsProvider + DebugAnalyticsProvider, 5 event chuẩn, check tên event, xếp hàng chờ khi SDK chưa sẵn sàng
- `Docs/quy-trinh-build-game.md` — từ điển thuật ngữ + quy trình chuẩn
- Scene demo `Demo_Core` + menu `HC Template > Dựng scene demo Core` để test 4 hệ thống Core (xoá khi làm game thật)

### Changed
### Fixed
### Removed

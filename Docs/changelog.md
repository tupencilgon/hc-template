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

### Changed
### Fixed
### Removed

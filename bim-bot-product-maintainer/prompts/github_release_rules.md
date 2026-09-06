# GitHub Release Rules for BIM-Bot

## Release Workflow

### 1. Version Bumping
- Follow Semantic Versioning: `MAJOR.MINOR.PATCH`
  - `PATCH`: Bug fixes, documentation updates, performance improvements.
  - `MINOR`: New tools added, new categories, non-breaking features.
  - `MAJOR`: Breaking architectural changes, major UI redesigns.

---

### 2. File Updates Prior to Release
Before tagging a release, update:
1. `version.json`:
   ```json
   {
     "version": "2.3.0",
     "releaseUrl": "https://github.com/HassanElmathary/BIM-Bot/releases/tag/v2.3.0",
     "downloadUrl": "https://github.com/HassanElmathary/BIM-Bot/releases/download/v2.3.0/BIMBot-Setup-2.3.0.exe",
     "assetFileName": "BIMBot-Setup-2.3.0.exe",
     "changelog": "BIM-Bot v2.3.0 — Summary of major additions.",
     "minVersion": "1.0.0"
   }
   ```
2. `installer/installer.iss`: Update `#define MyAppVersion "2.3.0"`.

---

### 3. Binary Compilation & Artifacts
- Compile `BIMBotPlugin.dll` for Revit C# handler.
- Run Inno Setup compiler to produce `installer/output/BIMBot-Setup-2.3.0.exe`.
- Verify `.exe` installer auto-detects Revit 2020–2027 and configures Claude Desktop key `"BIM-Bot"`.

---

### 4. Git Tagging & Release Creation
- Create Git tag: `git tag -a v2.3.0 -m "Release v2.3.0"`
- Push tag to remote: `git push origin v2.3.0`
- Create GitHub Release for `v2.3.0` with markdown release notes.
- Attach `BIMBot-Setup-2.3.0.exe` to the release assets.

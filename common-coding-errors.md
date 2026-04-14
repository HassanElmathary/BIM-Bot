# Common Coding Errors — BIM-Bot Plugin

A running log of hard-won debugging lessons. Check here before chasing ghosts.

---

## 1. Ribbon Icons Not Displaying (Blank / Tiny Fragments)

**Date:** 2026-04-05

### Symptoms
- Revit ribbon buttons show blank icons or tiny slivers instead of the full image.
- Icons appear as small colored dots in the top-left corner of the button area.

### Root Causes Found

#### A. Duplicate `.addin` Files — Revit Loads the Wrong DLL
There were **two** `BIMBot.addin` files on the system:

| Location | Path in `<Assembly>` |
|---|---|
| **System-level** `C:\ProgramData\Autodesk\Revit\Addins\2026\` | `C:\Program Files\BIMBot\plugin\net8\BIMBotPlugin.dll` |
| **User-level** `%APPDATA%\Autodesk\Revit\Addins\2026\` | `d:\...\bin\Release\net8.0-windows\BIMBotPlugin.dll` |

**The user-level addin takes precedence.** We kept deploying updates to `C:\Program Files\...` but Revit was silently loading the old DLL from `bin\Release\...`. Every deploy appeared to work but Revit never picked up the changes.

> [!CAUTION]
> **Always check BOTH addin locations:**
> - `C:\ProgramData\Autodesk\Revit\Addins\{version}\`
> - `%APPDATA%\Autodesk\Revit\Addins\{version}\`
>
> User-level wins when both exist with the same `ClientId`.

**Fix:** Build in **Release** config (`dotnet build -c Release`) since that's where the user-level `.addin` points. Or remove the duplicate.

#### B. `BitmapImage.DecodePixelWidth/Height` Does NOT Scale — It Crops
When loading embedded PNG icons via `BitmapImage`, setting `DecodePixelWidth = 32` on a 64×64 source image **crops to the top-left 32×32 pixels** instead of scaling. This produces tiny icon fragments.

```csharp
// ❌ WRONG — crops the image, doesn't scale it
var bmp = new BitmapImage();
bmp.BeginInit();
bmp.StreamSource = stream;
bmp.DecodePixelWidth = 32;   // crops!
bmp.DecodePixelHeight = 32;  // crops!
bmp.CacheOption = BitmapCacheOption.OnLoad;
bmp.EndInit();
```

```csharp
// ✅ CORRECT — load at native size, then scale with TransformedBitmap
var bmp = new BitmapImage();
bmp.BeginInit();
bmp.StreamSource = stream;
bmp.CacheOption = BitmapCacheOption.OnLoad;
bmp.EndInit();
bmp.Freeze();

double scaleX = (double)targetPx / bmp.PixelWidth;
double scaleY = (double)targetPx / bmp.PixelHeight;
var scaled = new TransformedBitmap(bmp, new ScaleTransform(scaleX, scaleY));
scaled.Freeze();
```

### Revit Ribbon Icon Size Requirements
| Property | Required Size |
|---|---|
| `LargeImage` | 32×32 px |
| `Image` | 16×16 px |

Revit **clips** (not scales) oversized images. Always provide exactly-sized bitmaps.

### Debugging Checklist
1. **Check which DLL Revit actually loads** — look for duplicate `.addin` files in both system and user directories.
2. **Verify DLL timestamp** — `dir "path\to\BIMBotPlugin.dll"` after every deploy.
3. **Build the correct config** — if `.addin` points to `bin\Release\`, then `dotnet build -c Release`.
4. **Fully close Revit** — File → Exit. Minimizing or closing the window may not unload the DLL.
5. **Check the plugin log** — `%APPDATA%\BIMBot\logs\bim-bot-YYYY-MM-DD.log` for `Resource not found` errors.

---

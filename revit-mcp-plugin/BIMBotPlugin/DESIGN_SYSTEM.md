# BIM-Bot Design System — "BIM Professional"

> **Single source of truth** for all colors, typography, spacing, and icon styles across the BIM-Bot Revit plugin.
> Last updated: 2026-04-11

---

## 🎨 Brand Color Palette

These colors are shared between **ribbon icons** (`Core/RibbonIcons.cs`) and **all WPF windows** (`UI/Themes/DarkTheme.cs`).

| Role        | Name          | Hex       | RGB              | C# Token            | Usage                              |
|-------------|---------------|-----------|------------------|----------------------|------------------------------------|
| **Primary** | Vivid Blue    | `#2563EB` | `37, 99, 235`    | `BrandPrimary`       | Main accent, buttons, active state |
| **Dark**    | Dark Blue     | `#1E40AF` | `30, 64, 175`    | `BrandDark`          | Pressed/hover, depth layers        |
| **Teal**    | Teal          | `#06B6D4` | `6, 182, 212`    | `BrandTeal`          | Secondary accent, info badges      |
| **Amber**   | Amber         | `#F59E0B` | `245, 158, 11`   | `BrandAmber`         | Highlights, warnings, sparkles     |
| **Green**   | Emerald       | `#10B981` | `16, 185, 129`   | `BrandGreen`         | Success, running, ON state         |
| **Red**     | Red           | `#EF4444` | `239, 68, 68`    | `BrandRed`           | Error, stopped, OFF state          |

---

## 🌑 Dark Mode Surfaces

| Role         | Hex       | RGB              | C# Token       | Usage                    |
|--------------|-----------|------------------|----------------|--------------------------|
| Canvas       | `#1A1B1E` | `26, 27, 30`     | `BgDark`       | Window background        |
| Card         | `#25262B` | `37, 38, 43`     | `BgCard`       | Panel / card fill        |
| Card Hover   | `#2C2E33` | `44, 46, 51`     | `BgCardHover`  | Card hover state         |
| Input        | `#1F2024` | `31, 32, 36`     | `BgInput`      | Input field background   |
| Header       | `#141517` | `20, 21, 23`     | `BgHeader`     | Title bars, footers      |
| Cancel Btn   | `#373A40` | `55, 58, 64`     | `BgCancel`     | Secondary buttons        |
| Cancel Hover | `#42454A` | `66, 69, 74`     | `BgCancelHover`| Secondary button hover   |

---

## ✏️ Text Colors

| Role        | Hex       | C# Token     | Usage                        |
|-------------|-----------|--------------|------------------------------|
| White       | `#FFFFFF` | `FgWhite`    | High-contrast text, headings |
| Light       | `#C1C2C5` | `FgLight`    | Primary body text            |
| Dim         | `#909296` | `FgDim`      | Placeholder, secondary text  |
| Required    | `#EF4444` | `FgRequired` | Required field markers       |
| Success     | `#10B981` | `FgGreen`    | Success messages             |
| Highlight   | `#F59E0B` | `FgGold`     | Highlighted / gold text      |
| Warning     | `#EF4444` | `FgWarning`  | Warning messages             |

---

## 📐 Borders & Dividers

| Role          | Hex       | C# Token       | Usage                   |
|---------------|-----------|-----------------|-------------------------|
| Subtle Border | `#373A40` | `BorderDim`     | Card borders, dividers  |
| Accent Border | `#2563EB` | `BorderAccent`  | Active/selected borders |
| Focus Ring    | `#06B6D4` | `BorderFocus`   | Focused input border    |

---

## 🏷️ Category Accents

Used in Tools Hub to color-code tool categories:

| Category     | Color        | Hex       | C# Token       |
|--------------|-------------|-----------|-----------------|
| Export       | Vivid Blue  | `#2563EB` | `CatExport`     |
| Families     | Amber       | `#F59E0B` | `CatFamily`     |
| Quick Views  | Emerald     | `#10B981` | `CatQuickView`  |
| Views/Sheets | Teal        | `#06B6D4` | `CatViewSheet`  |

---

## 🔤 Typography

| Property    | Value      |
|-------------|------------|
| Font Family | `Segoe UI` |
| Body Size   | `13px`     |
| Label Size  | `12px`     |
| Header Size | `14px`     |
| Weight      | `SemiBold` for headers, `Normal` for body |

---

## 📏 Spacing & Radii

| Token          | Value            | Usage                  |
|----------------|------------------|------------------------|
| `CardRadius`   | `8px`            | Card / group box       |
| `ButtonRadius` | `6px`            | Buttons                |
| `InputRadius`  | `4px`            | Input fields, combos   |
| `CardPadding`  | `14, 10, 14, 14` | Card inner padding     |

---

## 🖼️ Ribbon Icon Style Guide

All 12 ribbon icons are **programmatic vector drawings** in `Core/RibbonIcons.cs` — no PNG files.

### Style Rules
- **Method:** WPF `DrawingContext` → `RenderTargetBitmap` at exactly 96 DPI
- **Sizes:** `32×32` for `LargeImage`, `16×16` for `Image`
- **Fill style:** Solid flat shapes — no gradients, no 3D effects
- **Stroke:** Rounded caps (`PenLineCap.Round`), rounded joins (`PenLineJoin.Round`)
- **Palette:** Uses only the 6 brand colors above + `White`

### Icon Inventory

| # | Button         | Shape Description                         | Primary Color  |
|---|----------------|-------------------------------------------|----------------|
| 1 | Start BIM-Bot  | ▶ Play triangle inside circle             | Primary Blue   |
| 2 | AI Chat        | 💬 Speech bubble + amber sparkle          | Primary Blue   |
| 3 | Project Files  | 📁 Folder (dark) + white doc page         | Dark / Primary |
| 4 | Connect Claude | 🔗 Two connected nodes + sparkle          | Primary / Teal |
| 5 | Local AI       | 🧠 CPU chip with pins + teal core         | Primary / Teal |
| 6 | Tools Hub      | ⊞ 2×2 colored grid                       | All 4 colors   |
| 7 | Export         | ⬆ Arrow rising from U-shaped tray         | Primary Blue   |
| 8 | Families       | 📦 3 nested offset squares + white "+"    | Dark/Primary/Teal |
| 9 | Quick Views    | 👁 Eye with pupil + amber speed lines     | Primary / Amber|
| 10| Views & Sheets | 📄 3 stacked pages + white text lines     | Slate/Dark/Primary |
| 11| Settings       | ⚙ 8-tooth gear cog                       | Primary Blue   |
| 12| Check Updates  | 🔄 Circular arrow + teal download arrow   | Primary / Teal |

### Badge Overlays (Toggle State)
- **Running (ON):** Green circle (`#10B981`) + white checkmark → `WithCheckBadge()`
- **Stopped (OFF):** Red circle (`#EF4444`) + white X → `WithCrossBadge()`

---

## 📁 File Reference

| Purpose              | File Path                                |
|----------------------|------------------------------------------|
| **Design System**    | `UI/Themes/DarkTheme.cs`                 |
| **Ribbon Icons**     | `Core/RibbonIcons.cs`                    |
| **Chat Icons**       | `UI/Themes/ChatIcons.cs`                 |
| **This Reference**   | `DESIGN_SYSTEM.md` ← you are here       |

---

## ✅ Rules for New Windows / Features

1. **Always** call `DarkTheme.Apply(this)` in your window constructor
2. **Never** hardcode color values — use `DarkTheme.BgCard`, `DarkTheme.BrandPrimary`, etc.
3. **Use factory methods** — `MakePrimaryButton()`, `MakeTextBox()`, `MakeGroupBox()`, etc.
4. **New icons** must use only the 6 brand colors + white, drawn as vectors
5. **Category colors** must map to `CatExport`, `CatFamily`, `CatQuickView`, or `CatViewSheet`

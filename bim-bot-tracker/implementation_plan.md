# BIM-BOT: Offline Revit Parameter Tracker — Implementation Plan

## Problem Summary

Build a standalone Windows desktop application that scans folder trees for Revit files, extracts element parameters headlessly, stores historical snapshots in a local SQLite database, computes diffs (Added/Modified/Deleted), and outputs structured reports mirroring the source directory tree. **Must support Revit versions 2020–2027.**

---

## Extraction Strategy: Hybrid Dual-Engine

The app uses a **try-primary, fallback-secondary** approach to maximize version coverage:

```
For each file found:
  ┌─ Is it a .rvt file?
  │   ├─ Read version via OLE2 (BasicFileInfo stream)
  │   ├─ Attempt revit-extractor
  │   │   ├─ ✅ Success → extract parameters, continue
  │   │   └─ ❌ Fail → log warning, check for matching .ifc
  │   │       ├─ .ifc found alongside .rvt → parse IFC automatically
  │   │       └─ .ifc NOT found → mark as SKIPPED, warn user in log
  │   └─ Either way, store file metadata (version, size, modified)
  │
  └─ Is it a .ifc file (without matching .rvt)?
      └─ Parse via ifcopenshell → extract parameters, continue
```

**Why this works for Revit 2020–2027:**
- `revit-extractor` handles files compatible with the installed Revit version
- For incompatible versions (e.g., Revit 2027 files on a Revit 2024 machine), the user provides the IFC export as fallback
- IFC parsing via `ifcopenshell` is **completely version-agnostic** — no Revit dependency
- OLE2 version detection warns users upfront which files may need IFC fallback

---

## Technology Stack

| Component | Technology | Rationale |
|---|---|---|
| Language | Python 3.11+ | Matches revit-extractor requirements (3.9+) |
| GUI Framework | **PySide6** (Qt 6) | Professional UI, native tree views, progress bars, QThread |
| Primary Extractor | **revit-extractor** | Direct .rvt parameter extraction (headless) |
| Fallback Extractor | **ifcopenshell** | Version-agnostic IFC parameter extraction |
| OLE2 Metadata | **olefile** | Read .rvt file version without Revit |
| Database | **SQLite3** (stdlib) | Zero-config embedded database, atomic transactions |
| Data Processing | **pandas** | DataFrame operations for extraction results |
| Packaging | **PyInstaller** | Standalone .exe with all dependencies bundled |

---

## Proposed Changes

### Project Structure

```
BIM-BOT/
├── src/
│   ├── main.py                    # Application entry point
│   ├── app.py                     # QApplication setup, main window launch
│   │
│   ├── ui/                        # UI Layer
│   │   ├── __init__.py
│   │   ├── main_window.py         # Main application window
│   │   ├── folder_selector.py     # Root folder & output folder selection
│   │   ├── filter_panel.py        # Category/parameter filter config
│   │   ├── progress_panel.py      # Dual progress bars + file info
│   │   ├── log_console.py         # Scrollable live log terminal
│   │   ├── tree_view.py           # Directory tree visualization
│   │   ├── results_panel.py       # Diff results display
│   │   └── styles.py              # QSS stylesheet (dark theme)
│   │
│   ├── core/                      # Core Engine Layer
│   │   ├── __init__.py
│   │   ├── scanner.py             # Directory traversal & file discovery
│   │   ├── extractor.py           # Dual extraction engine (revit-extractor + IFC)
│   │   ├── rvt_extractor.py       # revit-extractor wrapper (primary)
│   │   ├── ifc_extractor.py       # ifcopenshell wrapper (fallback)
│   │   ├── rvt_metadata.py        # OLE2 .rvt file version/metadata reader
│   │   ├── diff_engine.py         # Differential comparison logic
│   │   ├── output_generator.py    # Report generation & folder mirroring
│   │   └── pipeline.py            # Orchestrates full scan pipeline
│   │
│   ├── db/                        # Database Layer
│   │   ├── __init__.py
│   │   ├── database.py            # SQLite connection, schema, migrations
│   │   ├── models.py              # Data classes for DB records
│   │   └── queries.py             # Parameterized SQL queries
│   │
│   └── workers/                   # Threading Layer
│       ├── __init__.py
│       └── scan_worker.py         # QThread-based background worker
│
├── tests/
│   ├── test_scanner.py
│   ├── test_rvt_extractor.py
│   ├── test_ifc_extractor.py
│   ├── test_diff_engine.py
│   ├── test_database.py
│   └── test_output_generator.py
│
├── assets/
│   └── icon.ico
│
├── requirements.txt
├── pyproject.toml
└── README.md
```

---

### Component 1: Directory & File System Engine

#### [NEW] [scanner.py](file:///c:/Users/h.elmthary/OneDrive/01-me/BIM-BOT/src/core/scanner.py)

- Recursive directory walker from user-selected root folder
- Long path support via `\\?\` prefix on Windows
- Discovers **both** `.rvt` and `.ifc` files
- Filters out lock files (`.~*`), backup files (`*.0001.rvt`), hidden/system files
- Auto-pairs `.rvt` files with matching `.ifc` files in the same directory
- All paths stored as **relative paths** from root folder
- Returns `ScanResult` with directory tree + file list + file metadata

---

### Component 2: Dual Extraction Engine

#### [NEW] [extractor.py](file:///c:/Users/h.elmthary/OneDrive/01-me/BIM-BOT/src/core/extractor.py)

Orchestrator that routes files to the correct extraction engine:

```python
class DualExtractor:
    def extract(self, file_path, filters) -> ExtractionResult:
        if file_path.endswith('.rvt'):
            # Try primary engine first
            try:
                return self.rvt_extractor.extract(file_path, filters)
            except Exception as e:
                self.log(f"⚠️ revit-extractor failed: {e}")
                # Look for matching .ifc fallback
                ifc_path = self._find_matching_ifc(file_path)
                if ifc_path:
                    self.log(f"↩️ Falling back to IFC: {ifc_path}")
                    return self.ifc_extractor.extract(ifc_path, filters)
                else:
                    self.log(f"❌ No IFC fallback found. Skipping.")
                    return ExtractionResult(status='SKIPPED', error=str(e))

        elif file_path.endswith('.ifc'):
            return self.ifc_extractor.extract(file_path, filters)
```

Returns a unified `ExtractionResult` dataclass regardless of which engine succeeded:
```python
@dataclass
class ExtractionResult:
    status: str           # 'SUCCESS', 'FALLBACK', 'SKIPPED', 'ERROR'
    engine_used: str      # 'revit-extractor' or 'ifcopenshell'
    elements: list        # List of ElementData
    element_count: int
    error: str = None
    warnings: list = None
```

#### [NEW] [rvt_extractor.py](file:///c:/Users/h.elmthary/OneDrive/01-me/BIM-BOT/src/core/rvt_extractor.py)

Primary engine wrapping `revit-extractor`:

```python
from revit_extract import RevitExtractor

class RvtExtractor:
    def extract(self, rvt_path, filters):
        prodb = RevitExtractor(rvt_path).read_prob_data()
        df = prodb.get_data_by_categories_and_params(
            filters.categories, filters.parameters
        )
        # Normalize: extract UniqueId, sanitize nulls, standardize types
        return self._to_extraction_result(df, engine='revit-extractor')
```

#### [NEW] [ifc_extractor.py](file:///c:/Users/h.elmthary/OneDrive/01-me/BIM-BOT/src/core/ifc_extractor.py)

Fallback engine wrapping `ifcopenshell`:

```python
import ifcopenshell

class IfcExtractor:
    # Maps Revit categories → IFC entity types
    CATEGORY_MAP = {
        "Walls": "IfcWall",
        "Doors": "IfcDoor",
        "Windows": "IfcWindow",
        "Structural Framing": "IfcBeam",
        "Structural Columns": "IfcColumn",
        "Floors": "IfcSlab",
        "Roofs": "IfcRoof",
        "Mechanical Equipment": "IfcFlowTerminal",
        "Pipes": "IfcFlowSegment",
        "Ducts": "IfcFlowSegment",
    }

    def extract(self, ifc_path, filters):
        model = ifcopenshell.open(ifc_path)
        elements = []
        for category, ifc_type in self.CATEGORY_MAP.items():
            if category in filters.categories:
                for el in model.by_type(ifc_type):
                    params = self._extract_psets(el, filters.parameters)
                    elements.append(ElementData(
                        unique_id=el.GlobalId,    # Equivalent to Revit UniqueId
                        category=category,
                        name=el.Name,
                        parameters=params
                    ))
        return ExtractionResult(status='SUCCESS', engine='ifcopenshell', ...)
```

#### [NEW] [rvt_metadata.py](file:///c:/Users/h.elmthary/OneDrive/01-me/BIM-BOT/src/core/rvt_metadata.py)

Reads `.rvt` file metadata via OLE2 **without Revit** (works for any version):

```python
import olefile

class RvtMetadata:
    def read(self, rvt_path) -> FileMetadata:
        ole = olefile.OleFileIO(rvt_path)
        basic_info = ole.openstream('BasicFileInfo').read()
        # Parse: Revit version, build, username, central path
        version = self._parse_version(basic_info)  # e.g., "2024"
        return FileMetadata(
            revit_version=version,
            file_size=os.path.getsize(rvt_path),
            modified=os.path.getmtime(rvt_path)
        )
```

Key use: **detect which Revit version created the file** so the UI can warn upfront if `revit-extractor` is likely to fail (e.g., file version > installed Revit version).

---

### Component 3: Local Database & State Management

#### [NEW] [database.py](file:///c:/Users/h.elmthary/OneDrive/01-me/BIM-BOT/src/db/database.py)

```sql
CREATE TABLE scan_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    root_folder TEXT NOT NULL,
    scan_start TIMESTAMP NOT NULL,
    scan_end TIMESTAMP,
    total_files INTEGER,
    files_succeeded INTEGER,
    files_skipped INTEGER,
    status TEXT DEFAULT 'IN_PROGRESS'
);

CREATE TABLE model_snapshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER REFERENCES scan_sessions(id),
    relative_file_path TEXT NOT NULL,
    file_size INTEGER,
    file_modified TIMESTAMP,
    revit_version TEXT,
    extraction_engine TEXT,        -- 'revit-extractor', 'ifcopenshell', 'skipped'
    scan_timestamp TIMESTAMP NOT NULL,
    element_count INTEGER,
    UNIQUE(session_id, relative_file_path)
);

CREATE TABLE element_parameters (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    relative_file_path TEXT NOT NULL,
    unique_id TEXT NOT NULL,
    category TEXT,
    element_name TEXT,
    parameter_name TEXT NOT NULL,
    parameter_value TEXT,
    last_updated TIMESTAMP NOT NULL,
    UNIQUE(relative_file_path, unique_id, parameter_name)
);

CREATE TABLE change_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER REFERENCES scan_sessions(id),
    relative_file_path TEXT NOT NULL,
    unique_id TEXT NOT NULL,
    category TEXT,
    change_type TEXT NOT NULL,     -- 'ADDED', 'MODIFIED', 'DELETED'
    parameter_name TEXT,
    old_value TEXT,
    new_value TEXT,
    detected_at TIMESTAMP NOT NULL
);
```

**Design features:**
- `extraction_engine` column tracks which engine processed each file
- Compound key: `relative_file_path + unique_id + parameter_name`
- All writes in `BEGIN TRANSACTION / COMMIT` for crash safety
- WAL journal mode for performance
- Database at `%AppData%/BIM-BOT/bim_tracker.db`

---

### Component 4: Differential (Diff) Engine

#### [NEW] [diff_engine.py](file:///c:/Users/h.elmthary/OneDrive/01-me/BIM-BOT/src/core/diff_engine.py)

| Condition | Classification | DB Action |
|---|---|---|
| UniqueId in scan but NOT in DB | **ADDED** | Insert `element_parameters`, log `change_log` |
| UniqueId in DB but NOT in scan | **DELETED** | Remove `element_parameters`, log `change_log` |
| UniqueId matches, value differs | **MODIFIED** | Update `element_parameters`, log old→new |
| UniqueId matches, value identical | **No Change** | Skip (no DB write) |

---

### Component 5: Output & Mirroring Engine

#### [NEW] [output_generator.py](file:///c:/Users/h.elmthary/OneDrive/01-me/BIM-BOT/src/core/output_generator.py)

- Mirrors source directory structure 1:1 into output folder
- Per-file reports in corresponding mirrored subfolder:
  - `{model_name}_changes.csv` — Added/Modified/Deleted with old/new values
  - `{model_name}_changes.json` — Same data in JSON
  - `{model_name}_snapshot.csv` — Full current parameter dump
- Global executive summary at output root:
  - `executive_summary.md` — Totals, highlights, errors, skipped files
  - `scan_log.txt` — Full timestamped processing log

---

### Component 6: GUI & Threading

#### [NEW] [main_window.py](file:///c:/Users/h.elmthary/OneDrive/01-me/BIM-BOT/src/ui/main_window.py)

```
┌─────────────────────────────────────────────────────────────┐
│  🏗️ BIM-BOT  Parameter Tracker              [─] [□] [×]   │
├─────────────────────────────────────────────────────────────┤
│  📂 Root Folder: [C:\Projects\BIM          ] [Browse...]    │
│  📁 Output Dir:  [C:\Projects\BIM\Output   ] [Browse...]    │
├──────────────────────┬──────────────────────────────────────┤
│  ▼ Filter Settings   │  Directory Tree                      │
│  ☑ Walls             │  📁 Phase1/                          │
│  ☑ Doors             │    📁 BuildingA/                     │
│  ☑ Windows           │      📄 Arch_Model.rvt (v2024, 45MB)│
│  ☑ Structural        │      📄 Arch_Model.ifc (paired ✓)   │
│  ☑ MEP Equipment     │      📄 Struct.rvt (v2020, 32MB)    │
│  ☐ Pipes             │    📁 BuildingB/                     │
│  ☐ Ducts             │      📄 MEP.rvt (v2027, 28MB) ⚠️    │
│                      │      📄 MEP.ifc (fallback ready ✓)   │
├──────────────────────┴──────────────────────────────────────┤
│  Overall: ████████████░░░░░░░░  58% (7/12 files)           │
│  Current: ██████████████████░░  89% Arch_Model.rvt (0:42)  │
├─────────────────────────────────────────────────────────────┤
│  [▶ Start]  [⏸ Pause]  [⏹ Cancel]         [📊 Export]      │
├─────────────────────────────────────────────────────────────┤
│  > Scanning \Phase1\BuildingA\Arch_Model.rvt (v2024)...     │
│  > ✅ revit-extractor: 1,247 elements, 5 categories        │
│  > 12 changes (8 modified, 3 added, 1 deleted)             │
│  > ⚠️ MEP.rvt (v2027): revit-extractor failed              │
│  > ↩️ Falling back to MEP.ifc → 834 elements extracted      │
│  > ❌ OldModel.rvt (v2020): failed, no .ifc fallback found  │
├─────────────────────────────────────────────────────────────┤
│  Files: 12 total │ ✅ 10 ok │ ↩️ 1 ifc │ ❌ 1 skip          │
└─────────────────────────────────────────────────────────────┘
```

**Key UI features for hybrid engine:**
- Tree view shows Revit version (detected via OLE2) and `.ifc` pairing status
- ⚠️ icon on files where Revit version > installed version (likely needs IFC fallback)
- Log console color-codes: ✅ green (success), ↩️ orange (IFC fallback), ❌ red (skipped)
- Status bar shows breakdown: succeeded / fallback / skipped

#### [NEW] [styles.py](file:///c:/Users/h.elmthary/OneDrive/01-me/BIM-BOT/src/ui/styles.py)

- Dark theme: `#0d1117` background, `#161b22` panels
- Accent: `#58a6ff` (blue), Success: `#3fb950` (green), Warning: `#d29922` (orange), Error: `#f85149` (red)
- Typography: Segoe UI + Cascadia Code (console)
- Glassmorphism panels, hover animations

#### [NEW] [scan_worker.py](file:///c:/Users/h.elmthary/OneDrive/01-me/BIM-BOT/src/workers/scan_worker.py)

- `QThread` with signals: `progress_updated`, `file_started`, `file_completed`, `log_message`, `scan_finished`
- Pause/Resume via `QMutex` + `QWaitCondition`
- Cancel via `is_cancelled` flag between files
- Sequential processing with `gc.collect()` between files

---

### Component 7: Pipeline Orchestrator

#### [NEW] [pipeline.py](file:///c:/Users/h.elmthary/OneDrive/01-me/BIM-BOT/src/core/pipeline.py)

```
1. Scan directories → build file list (.rvt + .ifc)
2. Pre-scan: read OLE2 version for all .rvt files
3. Warn user about version mismatches (files likely needing IFC fallback)
4. Begin DB transaction (scan session)
5. For each file:
   a. Check if locked → copy to %TEMP% if needed
   b. Route to DualExtractor (try revit-extractor → fallback IFC)
   c. Run diff engine against DB
   d. Update element_parameters (in transaction)
   e. Log changes to change_log
   f. Generate per-file report in mirrored output folder
   g. gc.collect()
   h. Emit progress signal
6. Generate executive summary
7. Commit transaction, finalize scan session
8. Clean up temp files
```

**Edge case handling:**

| Problem | Solution |
|---|---|
| File locked by Revit | Copy `.rvt` to `%TEMP%`, parse copy, delete copy |
| revit-extractor fails (version mismatch) | Auto-fallback to `.ifc` if available |
| No `.ifc` fallback available | Mark as SKIPPED, log clearly, continue to next |
| Corrupted `.rvt` or `.ifc` | try/catch per file, log error, skip to next |
| Same filename in different folders | Compound key `relative_path + unique_id` |
| Memory leaks on large scans | Sequential processing, `gc.collect()` per file |
| Crash mid-scan | Atomic SQL transactions, rollback to last file |
| Path > 260 chars | `\\?\` extended-length prefix |
| Mixed engines across scans | `extraction_engine` column tracks source per file |

---

## Verification Plan

### Automated Tests

```bash
python -m pytest tests/ -v
```

### Manual Verification

1. **Hybrid engine test**: Provide a mix of .rvt files (compatible + incompatible versions) and .ifc files. Verify correct engine routing and fallback behavior.
2. **Version detection**: Test OLE2 version reading across Revit 2020–2027 sample files.
3. **Diff engine**: Run scan twice with modified files → verify ADDED/MODIFIED/DELETED.
4. **Output structure**: Verify output directory mirrors input exactly.
5. **UI responsiveness**: Large folder scan, verify pause/cancel, responsive UI.
6. **Crash recovery**: Kill mid-scan, verify DB integrity.
7. **Standalone test**: PyInstaller `.exe` on clean Windows machine with Revit installed.

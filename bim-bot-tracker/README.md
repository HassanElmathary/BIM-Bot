# BIM-BOT — Offline Revit Parameter Tracker

A standalone Windows desktop application that scans folder trees for Revit (.rvt) files, extracts element parameters headlessly, stores historical snapshots in a local SQLite database, computes diffs (Added/Modified/Deleted), and outputs structured reports mirroring the source directory tree.

## Features

- **Hybrid Extraction Engine**: Uses `revit-extractor` for direct .rvt parsing with automatic fallback to `ifcopenshell` for IFC files
- **Revit 2020–2027 Support**: Auto-detects file version via OLE2, routes to best available engine
- **Change Tracking**: SQLite-backed diff engine detects Added, Modified, and Deleted parameters
- **Directory Mirroring**: Output reports mirror the exact source folder structure
- **Professional Dark UI**: PySide6 desktop app with real-time progress, live console, and tree view
- **Crash-Safe**: Atomic SQL transactions protect database integrity

## Requirements

- Windows 10/11
- Autodesk Revit (any version 2020–2027) installed
- Python 3.9+ (for development; standalone .exe requires no Python)

## Quick Start

```bash
# Install dependencies
pip install -r requirements.txt

# Run the application
python src/main.py
```

## Build Standalone Executable

```bash
pip install pyinstaller
pyinstaller build/bim_bot.spec --clean
```

## Project Structure

```
BIM-BOT/
├── src/
│   ├── main.py              # Entry point
│   ├── app.py               # QApplication setup
│   ├── ui/                  # GUI components
│   ├── core/                # Core engine (scanner, extractors, diff, pipeline)
│   ├── db/                  # SQLite database layer
│   └── workers/             # Background threading
├── tests/                   # Unit tests
├── assets/                  # Icons and resources
└── requirements.txt
```

## License

MIT

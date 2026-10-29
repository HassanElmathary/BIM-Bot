"""Dark theme styles for BIM-BOT."""

BG_PRIMARY = '#0d1117'
BG_SECONDARY = '#161b22'
BG_TERTIARY = '#21262d'
BORDER = '#30363d'
TEXT_PRIMARY = '#e6edf3'
TEXT_SECONDARY = '#8b949e'
TEXT_MUTED = '#8b949e'
ACCENT = '#58a6ff'
SUCCESS = '#3fb950'
WARNING = '#d29922'
ERROR = '#f85149'
SURFACE_HOVER = '#292e36'

DARK_THEME = f"""
QMainWindow {{
    background-color: {BG_PRIMARY};
    color: {TEXT_PRIMARY};
    font-family: 'Segoe UI';
}}

QWidget {{
    background-color: {BG_PRIMARY};
    color: {TEXT_PRIMARY};
    font-family: 'Segoe UI';
    font-size: 10pt;
}}

/* Groups and Frames */
QGroupBox {{
    border: 1px solid {BORDER};
    border-radius: 6px;
    margin-top: 1.5ex;
    font-weight: bold;
    background-color: {BG_SECONDARY};
}}

QGroupBox::title {{
    subcontrol-origin: margin;
    subcontrol-position: top left;
    left: 10px;
    padding: 0 3px;
    color: {TEXT_SECONDARY};
}}

QFrame {{
    border: 1px solid {BORDER};
    border-radius: 4px;
}}

/* Inputs */
QLineEdit, QTextEdit, QPlainTextEdit {{
    background-color: {BG_TERTIARY};
    border: 1px solid {BORDER};
    border-radius: 4px;
    padding: 4px;
    color: {TEXT_PRIMARY};
    selection-background-color: {ACCENT};
}}

QLineEdit:focus, QTextEdit:focus {{
    border: 1px solid {ACCENT};
}}

QLineEdit:disabled {{
    color: {TEXT_SECONDARY};
    background-color: {BG_SECONDARY};
}}

/* Buttons */
QPushButton {{
    background-color: {BG_TERTIARY};
    border: 1px solid {BORDER};
    border-radius: 4px;
    padding: 6px 12px;
    color: {TEXT_PRIMARY};
    font-weight: 500;
}}

QPushButton:hover {{
    background-color: {SURFACE_HOVER};
    border: 1px solid {ACCENT};
    transition: border-color 0.2s ease;
}}

QPushButton:pressed {{
    background-color: {BG_SECONDARY};
    border: 1px solid {ACCENT};
}}

QPushButton:disabled {{
    background-color: {BG_SECONDARY};
    border: 1px solid {BORDER};
    color: {TEXT_SECONDARY};
}}

/* Progress Bar */
QProgressBar {{
    border: 1px solid {BORDER};
    border-radius: 4px;
    background-color: {BG_TERTIARY};
    text-align: center;
    color: {TEXT_PRIMARY};
}}

QProgressBar::chunk {{
    background-color: qlineargradient(spread:pad, x1:0, y1:0, x2:1, y2:0, 
                                      stop:0 {ACCENT}, stop:1 #1f6feb);
    border-radius: 3px;
}}

/* Tree and Table Views */
QTreeView, QTableView {{
    background-color: {BG_SECONDARY};
    alternate-background-color: {BG_TERTIARY};
    border: 1px solid {BORDER};
    border-radius: 4px;
    outline: none;
}}

QTreeView::item:hover, QTableView::item:hover {{
    background-color: {SURFACE_HOVER};
}}

QTreeView::item:selected, QTableView::item:selected {{
    background-color: {BG_TERTIARY};
    color: {ACCENT};
    border: 1px solid {ACCENT};
}}

QHeaderView::section {{
    background-color: {BG_TERTIARY};
    color: {TEXT_SECONDARY};
    border: none;
    border-bottom: 1px solid {BORDER};
    border-right: 1px solid {BORDER};
    padding: 4px;
    font-weight: bold;
}}

/* Checkboxes */
QCheckBox {{
    spacing: 8px;
}}
QCheckBox::indicator {{
    width: 16px;
    height: 16px;
    border: 1px solid {BORDER};
    border-radius: 3px;
    background-color: {BG_TERTIARY};
}}
QCheckBox::indicator:hover {{
    border: 1px solid {ACCENT};
}}
QCheckBox::indicator:checked {{
    background-color: {ACCENT};
    border: 1px solid {ACCENT};
    image: url(data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAxNiAxNiI+PHBhdGggZmlsbD0iI2ZmZmZmZiIgZD0iTTEzLjc4IDQuMjJsLTEuMDYtMS4wNmwtNi45NyA2Ljk3bC0zLjU2LTMuNTZsLTEuMDYgMS4wNmw0LjYyIDQuNjJ6Ii8+PC9zdmc+);
}}

/* Scrollbars */
QScrollBar:vertical {{
    border: none;
    background: {BG_PRIMARY};
    width: 10px;
    margin: 0px;
}}
QScrollBar::handle:vertical {{
    background: {BORDER};
    min-height: 20px;
    border-radius: 5px;
}}
QScrollBar::handle:vertical:hover {{
    background: {TEXT_SECONDARY};
}}
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{
    height: 0px;
}}

QScrollBar:horizontal {{
    border: none;
    background: {BG_PRIMARY};
    height: 10px;
    margin: 0px;
}}
QScrollBar::handle:horizontal {{
    background: {BORDER};
    min-width: 20px;
    border-radius: 5px;
}}
QScrollBar::handle:horizontal:hover {{
    background: {TEXT_SECONDARY};
}}
QScrollBar::add-line:horizontal, QScrollBar::sub-line:horizontal {{
    width: 0px;
}}

/* Splitter */
QSplitter::handle {{
    background-color: {BORDER};
    margin: 2px;
}}
QSplitter::handle:horizontal {{
    width: 2px;
}}
QSplitter::handle:vertical {{
    height: 2px;
}}
QSplitter::handle:hover {{
    background-color: {ACCENT};
}}

/* StatusBar */
QStatusBar {{
    background-color: {BG_SECONDARY};
    color: {TEXT_SECONDARY};
    border-top: 1px solid {BORDER};
}}

/* ToolTip */
QToolTip {{
    background-color: {BG_TERTIARY};
    color: {TEXT_PRIMARY};
    border: 1px solid {BORDER};
    border-radius: 4px;
    padding: 4px;
}}
"""

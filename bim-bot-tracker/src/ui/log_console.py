"""Log console widget for live logging output."""
from datetime import datetime
from PySide6.QtWidgets import QTextEdit, QMenu
from PySide6.QtGui import QFont, QTextCursor, QAction
from PySide6.QtCore import Qt

from ui.styles import BG_PRIMARY, SUCCESS, WARNING, ERROR, ACCENT, TEXT_PRIMARY

class LogConsole(QTextEdit):
    """A read-only text edit for displaying logs with color-coded severity."""
    
    MAX_LINES = 10000
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setReadOnly(True)
        
        font = QFont("Cascadia Code", 10)
        font.setStyleHint(QFont.StyleHint.Monospace)
        self.setFont(font)
        
        self.setStyleSheet(f"background-color: {BG_PRIMARY}; padding: 4px; border-radius: 4px;")
        self.setContextMenuPolicy(Qt.ContextMenuPolicy.CustomContextMenu)
        self.customContextMenuRequested.connect(self._show_context_menu)
        self._line_count = 0

    def _format_msg(self, msg: str, color: str) -> str:
        timestamp = datetime.now().strftime("%H:%M:%S")
        return f'<span style="color: #8b949e">[{timestamp}]</span> <span style="color: {color}">{msg}</span>'

    def _append_html(self, html: str):
        self.moveCursor(QTextCursor.MoveOperation.End)
        self.insertHtml(html + "<br>")
        self.moveCursor(QTextCursor.MoveOperation.End)
        self._line_count += 1
        # Trim old lines if over limit
        if self._line_count > self.MAX_LINES:
            cursor = self.textCursor()
            cursor.movePosition(QTextCursor.MoveOperation.Start)
            cursor.movePosition(QTextCursor.MoveOperation.Down, QTextCursor.MoveMode.KeepAnchor, 100)
            cursor.removeSelectedText()
            self._line_count -= 100

    def append_info(self, msg: str):
        self._append_html(self._format_msg(msg, ACCENT))

    def append_success(self, msg: str):
        self._append_html(self._format_msg(msg, SUCCESS))

    def append_warning(self, msg: str):
        self._append_html(self._format_msg(msg, WARNING))

    def append_error(self, msg: str):
        self._append_html(self._format_msg(msg, ERROR))

    def append_fallback(self, msg: str):
        # Yellow-orange for fallback
        self._append_html(self._format_msg(msg, "#e3b341"))
        
    def clear_log(self):
        self.clear()

    def _show_context_menu(self, pos):
        menu = QMenu(self)
        
        copy_action = QAction("Copy", self)
        copy_action.triggered.connect(self.copy)
        menu.addAction(copy_action)
        
        select_all_action = QAction("Select All", self)
        select_all_action.triggered.connect(self.selectAll)
        menu.addAction(select_all_action)
        
        clear_action = QAction("Clear", self)
        clear_action.triggered.connect(self.clear_log)
        menu.addAction(clear_action)
        
        save_action = QAction("Save Log to File", self)
        save_action.triggered.connect(self.export_log)
        menu.addAction(save_action)
        
        menu.exec(self.mapToGlobal(pos))

    def export_log(self):
        from PySide6.QtWidgets import QFileDialog
        path, _ = QFileDialog.getSaveFileName(self, "Save Log", "", "Text Files (*.txt);;All Files (*)")
        if path:
            try:
                with open(path, 'w', encoding='utf-8') as f:
                    f.write(self.toPlainText())
            except Exception as e:
                self.append_error(f"Failed to export log: {e}")

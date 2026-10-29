"""QApplication setup for BIM-BOT."""
import sys
from PySide6.QtWidgets import QApplication
from PySide6.QtCore import Qt
from PySide6.QtGui import QFont

from ui.main_window import MainWindow
from ui.styles import DARK_THEME


def run():
    """Initialize and run the QApplication."""
    app = QApplication(sys.argv)
    app.setApplicationName("BIM-BOT")
    app.setOrganizationName("BIM-BOT")

    # Apply theme and fonts
    app.setStyleSheet(DARK_THEME)
    font = QFont("Segoe UI", 10)
    app.setFont(font)

    window = MainWindow()
    window.show()

    sys.exit(app.exec())

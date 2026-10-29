"""Progress tracking panel."""
from PySide6.QtWidgets import QWidget, QVBoxLayout, QHBoxLayout, QLabel, QProgressBar
from PySide6.QtCore import QTimer

class ProgressPanel(QWidget):
    """Panel displaying overall and current file progress."""
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self._setup_ui()
        
    def _setup_ui(self):
        layout = QVBoxLayout(self)
        layout.setContentsMargins(10, 5, 10, 5)
        layout.setSpacing(5)
        
        # Overall Progress
        overall_layout = QHBoxLayout()
        overall_lbl = QLabel("Overall:")
        overall_lbl.setFixedWidth(60)
        self.overall_bar = QProgressBar()
        self.overall_bar.setFormat("%p%")
        self.overall_text = QLabel("0/0 files")
        overall_layout.addWidget(overall_lbl)
        overall_layout.addWidget(self.overall_bar)
        overall_layout.addWidget(self.overall_text)
        
        # Current File Progress
        file_layout = QHBoxLayout()
        file_lbl = QLabel("Current:")
        file_lbl.setFixedWidth(60)
        self.file_bar = QProgressBar()
        self.file_bar.setFormat("%p%")
        self.file_info = QLabel("Idle")
        file_layout.addWidget(file_lbl)
        file_layout.addWidget(self.file_bar)
        file_layout.addWidget(self.file_info)
        
        layout.addLayout(overall_layout)
        layout.addLayout(file_layout)
        
    def set_overall(self, current: int, total: int):
        self.overall_bar.setMaximum(total)
        self.overall_bar.setValue(current)
        self.overall_text.setText(f"{current}/{total} files")
        
    def set_file(self, filename: str, size_str: str, progress_pct: int, elapsed_str: str):
        self.file_bar.setValue(progress_pct)
        self.file_info.setText(f"{filename} ({size_str}) - {elapsed_str}")
        
    def reset(self):
        self.overall_bar.setValue(0)
        self.overall_bar.setMaximum(100)
        self.overall_text.setText("0/0 files")
        self.file_bar.setValue(0)
        self.file_info.setText("Idle")

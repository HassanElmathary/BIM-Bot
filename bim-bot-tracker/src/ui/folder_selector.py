"""Folder selection panel."""
import os
from PySide6.QtWidgets import QWidget, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit, QPushButton, QFileDialog
from PySide6.QtCore import Signal, QSettings

class FolderSelector(QWidget):
    """Panel for selecting root and output directories."""
    
    root_folder_changed = Signal(str)
    output_folder_changed = Signal(str)

    def __init__(self, parent=None):
        super().__init__(parent)
        self.settings = QSettings("BIM-BOT", "Settings")
        self._setup_ui()
        self._load_settings()

    def _setup_ui(self):
        layout = QVBoxLayout(self)
        layout.setContentsMargins(10, 10, 10, 10)
        
        # Root Folder Row
        root_layout = QHBoxLayout()
        root_label = QLabel("📂 Root Folder:")
        root_label.setFixedWidth(100)
        self.root_edit = QLineEdit()
        self.root_edit.setReadOnly(True)
        self.root_btn = QPushButton("Browse...")
        self.root_btn.clicked.connect(self._browse_root)
        root_layout.addWidget(root_label)
        root_layout.addWidget(self.root_edit)
        root_layout.addWidget(self.root_btn)
        
        # Output Directory Row
        out_layout = QHBoxLayout()
        out_label = QLabel("📁 Output Dir:")
        out_label.setFixedWidth(100)
        self.out_edit = QLineEdit()
        self.out_edit.setReadOnly(True)
        self.out_btn = QPushButton("Browse...")
        self.out_btn.clicked.connect(self._browse_out)
        out_layout.addWidget(out_label)
        out_layout.addWidget(self.out_edit)
        out_layout.addWidget(self.out_btn)
        
        # Database Path Display
        self.db_label = QLabel("Database Path: Not set")
        self.db_label.setStyleSheet("color: #8b949e; font-size: 9pt;")
        
        layout.addLayout(root_layout)
        layout.addLayout(out_layout)
        layout.addWidget(self.db_label)
        
    def _browse_root(self):
        start_dir = self.root_edit.text() or os.path.expanduser("~")
        folder = QFileDialog.getExistingDirectory(self, "Select Root Folder", start_dir)
        if folder and os.path.exists(folder):
            self.root_edit.setText(folder)
            self.settings.setValue("root_folder", folder)
            self.root_folder_changed.emit(folder)
            self._update_db_path()

    def _browse_out(self):
        start_dir = self.out_edit.text() or os.path.expanduser("~")
        folder = QFileDialog.getExistingDirectory(self, "Select Output Directory", start_dir)
        if folder and os.path.exists(folder):
            self.out_edit.setText(folder)
            self.settings.setValue("output_folder", folder)
            self.output_folder_changed.emit(folder)
            self._update_db_path()

    def _load_settings(self):
        root = self.settings.value("root_folder", "")
        out = self.settings.value("output_folder", "")
        
        if root and os.path.exists(root):
            self.root_edit.setText(root)
            self.root_folder_changed.emit(root)
            
        if out and os.path.exists(out):
            self.out_edit.setText(out)
            self.output_folder_changed.emit(out)
            
        self._update_db_path()

    def _update_db_path(self):
        out_folder = self.out_edit.text()
        if out_folder:
            db_path = os.path.join(out_folder, "bimbot_data.db")
            self.db_label.setText(f"Database Path: {db_path}")
        else:
            self.db_label.setText("Database Path: Not set")
            
    def get_root_folder(self) -> str:
        return self.root_edit.text()
        
    def get_output_folder(self) -> str:
        return self.out_edit.text()

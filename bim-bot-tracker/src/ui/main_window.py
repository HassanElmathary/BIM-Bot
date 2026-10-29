"""Main application window."""
import os
from datetime import datetime
from PySide6.QtWidgets import (
    QMainWindow, QWidget, QVBoxLayout, QHBoxLayout, 
    QSplitter, QTabWidget, QPushButton, QStatusBar
)
from PySide6.QtCore import Qt, QSettings
from PySide6.QtGui import QIcon

from ui.folder_selector import FolderSelector
from ui.filter_panel import FilterPanel
from ui.progress_panel import ProgressPanel
from ui.tree_view import FileTreeView
from ui.results_panel import ResultsPanel, DiffReportRow
from ui.log_console import LogConsole
from workers.scan_worker import ScanWorker
from ui.styles import ACCENT
from db.database import BimDatabase


class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("BIM-BOT — Parameter Tracker & Audit")
        self.setMinimumSize(1250, 850)
        self.setWindowIcon(QIcon("assets/icon.ico"))
        
        self.settings = QSettings("BIM-BOT", "Geometry")
        self.worker = None
        self._setup_ui()
        self._restore_geometry()
        
        # Try loading database if output folder is set from previous session
        self._try_load_db()

    def _setup_ui(self):
        central = QWidget()
        self.setCentralWidget(central)
        main_layout = QVBoxLayout(central)
        
        # Top Panel
        self.folder_selector = FolderSelector()
        self.folder_selector.output_folder_changed.connect(self._on_output_folder_changed)
        main_layout.addWidget(self.folder_selector)
        
        # Splitter for Main Content
        self.main_splitter = QSplitter(Qt.Orientation.Vertical)
        
        # Middle Section (Filters + Tabs)
        mid_widget = QWidget()
        mid_layout = QHBoxLayout(mid_widget)
        mid_layout.setContentsMargins(0, 0, 0, 0)
        
        self.filter_panel = FilterPanel()
        self.filter_panel.setFixedWidth(250)
        
        self.tabs = QTabWidget()
        self.tree_view = FileTreeView()
        self.results_panel = ResultsPanel()
        
        self.tabs.addTab(self.tree_view, "📁 Directory Tree")
        self.tabs.addTab(self.results_panel, "📊 Parameter Audit & Results")
        
        mid_layout.addWidget(self.filter_panel)
        mid_layout.addWidget(self.tabs)
        
        self.main_splitter.addWidget(mid_widget)
        
        # Console
        self.console = LogConsole()
        self.main_splitter.addWidget(self.console)
        
        # Adjust splitter sizes
        self.main_splitter.setSizes([600, 200])
        main_layout.addWidget(self.main_splitter)
        
        # Progress and Controls
        self.progress_panel = ProgressPanel()
        main_layout.addWidget(self.progress_panel)
        
        controls_layout = QHBoxLayout()

        self.btn_load_db = QPushButton("📂 Load Results DB")
        self.btn_load_db.clicked.connect(self._try_load_db)

        self.btn_start = QPushButton("▶ Start Scan")
        self.btn_start.setStyleSheet(f"background-color: {ACCENT}; color: #000; font-weight: bold;")
        self.btn_start.clicked.connect(self._start_scan)
        
        self.btn_pause = QPushButton("⏸ Pause")
        self.btn_pause.setEnabled(False)
        self.btn_pause.clicked.connect(self._toggle_pause)
        
        self.btn_cancel = QPushButton("⏹ Cancel")
        self.btn_cancel.setEnabled(False)
        self.btn_cancel.clicked.connect(self._cancel_scan)
        
        controls_layout.addWidget(self.btn_load_db)
        controls_layout.addStretch()
        controls_layout.addWidget(self.btn_start)
        controls_layout.addWidget(self.btn_pause)
        controls_layout.addWidget(self.btn_cancel)
        main_layout.addLayout(controls_layout)
        
        # Status Bar
        self.status_bar = QStatusBar()
        self.setStatusBar(self.status_bar)
        self.status_bar.showMessage("Ready")

    def _get_db_path(self) -> str:
        out = self.folder_selector.get_output_folder()
        if not out:
            return ""
        # Check bimbot_data.db or bimbot.db
        path1 = os.path.join(out, "bimbot_data.db")
        path2 = os.path.join(out, "bimbot.db")
        if os.path.exists(path1):
            return path1
        if os.path.exists(path2):
            return path2
        return path1

    def _on_output_folder_changed(self, folder: str):
        self._try_load_db()

    def _try_load_db(self):
        db_path = self._get_db_path()
        if not db_path or not os.path.exists(db_path):
            return

        try:
            with BimDatabase(db_path) as db:
                params = db.get_all_parameters()
                logs = db.get_all_change_logs()

                self.results_panel.set_parameter_records(params)

                diff_rows = [
                    DiffReportRow(
                        file_path=c.relative_file_path,
                        element_id=c.unique_id,
                        category=c.category,
                        change_type=c.change_type,
                        parameter=c.parameter_name,
                        old_value=c.old_value,
                        new_value=c.new_value
                    ) for c in logs
                ]
                self.results_panel.set_diff_results(diff_rows)

            self.console.append_info(
                f"Loaded database: {len(params)} parameter records, {len(logs)} change logs."
            )
        except Exception as e:
            self.console.append_warning(f"Could not load database {db_path}: {e}")

    def _start_scan(self):
        root = self.folder_selector.get_root_folder()
        out = self.folder_selector.get_output_folder()
        
        if not root or not out:
            self.console.append_error("Please select both Root and Output folders.")
            return
            
        self.console.clear_log()
        self.console.append_info("Starting scan...")
        
        self.btn_start.setEnabled(False)
        self.btn_pause.setEnabled(True)
        self.btn_cancel.setEnabled(True)
        
        config = self.filter_panel.get_config()
        db_path = os.path.join(out, "bimbot_data.db")
        
        self.worker = ScanWorker(root, out, config, db_path)
        self.worker.log_message.connect(self._handle_log)
        self.worker.progress_updated.connect(self.progress_panel.set_overall)
        self.worker.file_started.connect(self._handle_file_started)
        self.worker.tree_status_update.connect(self.tree_view.update_file_status)
        self.worker.scan_finished.connect(self._handle_scan_finished)
        
        self.worker.start()

    def _toggle_pause(self):
        if self.worker:
            if self.worker.is_paused:
                self.worker.resume()
                self.btn_pause.setText("⏸ Pause")
            else:
                self.worker.pause()
                self.btn_pause.setText("▶ Resume")

    def _cancel_scan(self):
        if self.worker:
            self.worker.cancel()
            self.console.append_warning("Cancelling scan...")
            self.btn_cancel.setEnabled(False)

    def _handle_log(self, msg: str, level: str):
        if level == 'info': self.console.append_info(msg)
        elif level == 'success': self.console.append_success(msg)
        elif level == 'warning': self.console.append_warning(msg)
        elif level == 'error': self.console.append_error(msg)
        elif level == 'fallback': self.console.append_fallback(msg)

    def _handle_file_started(self, filename: str, size: str):
        self.progress_panel.set_file(filename, size, 0, "0:00")

    def _handle_scan_finished(self, success: bool, msg: str):
        self.btn_start.setEnabled(True)
        self.btn_pause.setEnabled(False)
        self.btn_cancel.setEnabled(False)
        self.btn_pause.setText("⏸ Pause")
        self.status_bar.showMessage(f"Last scan: {datetime.now().strftime('%H:%M:%S')}")

        # Automatically load DB results after scan completes
        self._try_load_db()
        self.tabs.setCurrentIndex(1)  # Switch to Results / Parameter Audit tab
        
    def _restore_geometry(self):
        geom = self.settings.value("geometry")
        if geom:
            self.restoreGeometry(geom)
            
    def closeEvent(self, event):
        self.settings.setValue("geometry", self.saveGeometry())
        if self.worker and self.worker.isRunning():
            self.worker.cancel()
            self.worker.wait()
        super().closeEvent(event)

"""Scan worker thread for background pipeline execution."""
import os
import logging
from typing import Any
from PySide6.QtCore import QThread, Signal, QMutex, QWaitCondition

from core.pipeline import ScanPipeline
from core.models import FilterConfig
from db.database import BimDatabase

logger = logging.getLogger(__name__)


class ScanWorker(QThread):
    """Background thread for running the scan pipeline.
    
    Bridges the ScanPipeline (core engine) with the Qt GUI by converting
    pipeline callbacks into Qt signals for thread-safe UI updates.
    """

    progress_updated = Signal(int, int)       # current_file_idx, total_files
    file_started = Signal(str, str)           # filename, size_str
    file_completed = Signal(str, object)      # filename, DiffReport or None
    log_message = Signal(str, str)            # message, level
    scan_finished = Signal(bool, str)         # success, summary_message
    tree_status_update = Signal(str, str)     # relative_path, status

    def __init__(self, root_folder: str, output_folder: str,
                 filter_config: Any, db_path: str):
        super().__init__()
        self.root_folder = root_folder
        self.output_folder = output_folder
        self.filter_config = filter_config
        self.db_path = db_path

        self.is_cancelled = False
        self.is_paused = False
        self._mutex = QMutex()
        self._wait_condition = QWaitCondition()
        self._pipeline = None

    def run(self):
        """Execute the full scan pipeline in the background thread."""
        try:
            # Initialize database
            db = BimDatabase(self.db_path)
            db.connect()

            # Build filter config if it's a dict/object from the UI
            if isinstance(self.filter_config, FilterConfig):
                config = self.filter_config
            else:
                # The UI filter panel returns a FilterConfig already
                config = self.filter_config

            # Create pipeline with callbacks
            self._pipeline = ScanPipeline(
                root_folder=self.root_folder,
                output_folder=self.output_folder,
                filter_config=config,
                db_interface=db,
                log_callback=self._on_log,
                progress_callback=self._on_progress,
                file_started_callback=self._on_file_started,
                file_completed_callback=self._on_file_completed,
            )

            # Run the pipeline
            self._emit_log("Initializing scanner...", "info")
            self._pipeline.run()

            if self.is_cancelled:
                self._emit_log("Scan cancelled by user.", "warning")
                self.scan_finished.emit(False, "Cancelled")
            else:
                self._emit_log("Scan completed successfully.", "success")
                self.scan_finished.emit(True, "Completed")

        except Exception as e:
            logger.error("Pipeline error", exc_info=True)
            self._emit_log(f"Pipeline error: {e}", "error")
            self.scan_finished.emit(False, str(e))
        finally:
            try:
                db.close()
            except Exception:
                pass

    def _on_log(self, message: str):
        """Pipeline log callback -> emit as Qt signal."""
        self._emit_log(message, "info")

    def _on_progress(self, current: int, total: int):
        """Pipeline progress callback -> emit as Qt signal."""
        self._check_pause()
        if self.is_cancelled and self._pipeline:
            self._pipeline.cancel()
        self.progress_updated.emit(current, total)

    def _on_file_started(self, relative_path: str):
        """Pipeline file started callback -> emit as Qt signal."""
        self.tree_status_update.emit(relative_path, "processing")
        try:
            size = os.path.getsize(
                os.path.join(self.root_folder, relative_path)
            )
            size_str = self._format_size(size)
        except Exception:
            size_str = "Unknown"
        self.file_started.emit(relative_path, size_str)

    def _on_file_completed(self, relative_path: str, status: str):
        """Pipeline file completed callback -> emit as Qt signal."""
        status_map = {
            'SUCCESS': 'success',
            'FALLBACK': 'fallback',
            'SKIPPED': 'skipped',
            'ERROR': 'error',
        }
        tree_status = status_map.get(status, 'error')
        self.tree_status_update.emit(relative_path, tree_status)

        if status == 'SUCCESS':
            self._emit_log(f"✅ Completed {relative_path}", "success")
        elif status == 'FALLBACK':
            self._emit_log(f"↩️ Completed via IFC fallback: {relative_path}", "fallback")
        elif status == 'SKIPPED':
            self._emit_log(f"⏭️ Skipped {relative_path}", "warning")
        else:
            self._emit_log(f"❌ Error processing {relative_path}", "error")

        self.file_completed.emit(relative_path, None)

    def _emit_log(self, message: str, level: str):
        """Thread-safe log emission."""
        self.log_message.emit(message, level)

    def _check_pause(self):
        """Block if paused, wake on resume."""
        self._mutex.lock()
        if self.is_paused:
            self._emit_log("Scan paused.", "warning")
            self._wait_condition.wait(self._mutex)
            self._emit_log("Scan resumed.", "info")
        self._mutex.unlock()

    def pause(self):
        """Pause the scan."""
        self._mutex.lock()
        self.is_paused = True
        self._mutex.unlock()
        if self._pipeline:
            self._pipeline.pause()

    def resume(self):
        """Resume the scan."""
        self._mutex.lock()
        self.is_paused = False
        self._wait_condition.wakeAll()
        self._mutex.unlock()
        if self._pipeline:
            self._pipeline.resume()

    def cancel(self):
        """Cancel the scan."""
        self.is_cancelled = True
        if self._pipeline:
            self._pipeline.cancel()
        self.resume()  # Wake up if paused so it can exit

    @staticmethod
    def _format_size(size_bytes: int) -> str:
        """Format file size in human-readable form."""
        if size_bytes < 1024:
            return f"{size_bytes} B"
        elif size_bytes < 1024 * 1024:
            return f"{size_bytes / 1024:.1f} KB"
        elif size_bytes < 1024 * 1024 * 1024:
            return f"{size_bytes / (1024 * 1024):.1f} MB"
        else:
            return f"{size_bytes / (1024 * 1024 * 1024):.1f} GB"

import os
import shutil
import tempfile
import threading
import gc
import logging
from typing import Callable, Optional, List, Dict
from .models import FilterConfig, FileInfo
from .scanner import Scanner
from .extractor import DualExtractor
from .diff_engine import DiffEngine
from .output_generator import OutputGenerator

logger = logging.getLogger(__name__)

class ScanPipeline:
    """Orchestrates the complete scan -> extract -> diff -> output flow."""
    
    def __init__(self, 
                 root_folder: str, 
                 output_folder: str,
                 filter_config: FilterConfig,
                 db_interface=None,  # Placeholder for DB access
                 log_callback: Optional[Callable[[str], None]] = None,
                 progress_callback: Optional[Callable[[int, int], None]] = None,
                 file_started_callback: Optional[Callable[[str], None]] = None,
                 file_completed_callback: Optional[Callable[[str, str], None]] = None):
        
        self.root_folder = root_folder
        self.output_folder = output_folder
        self.filter_config = filter_config
        self.db = db_interface
        
        self.log_callback = log_callback
        self.progress_callback = progress_callback
        self.file_started_callback = file_started_callback
        self.file_completed_callback = file_completed_callback
        
        self.pause_event = threading.Event()
        self.pause_event.set() # Set means NOT paused
        self.cancel_event = threading.Event()

    def _log(self, message: str):
        logger.info(message)
        if self.log_callback:
            self.log_callback(message)

    def _handle_locked_file(self, file_path: str) -> str:
        """Copies file to temp dir if it's locked, returns new path."""
        try:
            # Try to open to check if locked
            with open(file_path, 'rb'):
                pass
            return file_path
        except PermissionError:
            self._log(f"File {file_path} is locked. Copying to temp directory...")
            temp_dir = tempfile.gettempdir()
            temp_path = os.path.join(temp_dir, os.path.basename(file_path))
            shutil.copy2(file_path, temp_path)
            return temp_path
        except Exception as e:
            self._log(f"Error accessing {file_path}: {e}")
            return file_path

    def run(self):
        self._log("Pipeline started.")
        self.cancel_event.clear()
        
        scanner = Scanner(self.root_folder)
        scan_result = scanner.scan()
        
        files_to_process = [f for f in scan_result.files if f.file_type in ('rvt', 'ifc')]
        total_files = len(files_to_process)
        
        self._log(f"Found {total_files} models to process.")
        
        # ── Phase 1: Cloud file download ──────────────────────────────
        cloud_files = [f for f in files_to_process if f.cloud_state == 'online']
        if cloud_files:
            self._log(f"☁️ {len(cloud_files)} files are cloud-only (Autodesk Desktop Connector). Downloading...")
            
            from .cloud_files import ensure_files_downloaded
            cloud_paths = [f.absolute_path for f in cloud_files]
            
            downloaded, failed = ensure_files_downloaded(
                file_paths=cloud_paths,
                log_callback=self.log_callback,
                progress_callback=self.progress_callback,
                file_progress_callback=lambda name, status: self._log(f"  {name}: {status}"),
                cancel_check=lambda: self.cancel_event.is_set(),
                timeout_per_file=600  # 10 min per file for large .rvt
            )
            
            # Update cloud states for downloaded files
            downloaded_set = set(downloaded)
            failed_set = set(failed)
            for f in files_to_process:
                if f.absolute_path in downloaded_set:
                    f.cloud_state = 'local'
                elif f.absolute_path in failed_set:
                    f.cloud_state = 'failed'
            
            # Remove files that failed to download
            if failed:
                self._log(f"⚠️ {len(failed)} files could not be downloaded. They will be skipped.")
                files_to_process = [f for f in files_to_process if f.cloud_state != 'failed']
                total_files = len(files_to_process)
            
            self._log(f"✅ All cloud files downloaded. Proceeding with {total_files} files.")
            
            if self.cancel_event.is_set():
                self._log("Pipeline cancelled during download phase.")
                return
        
        # ── Phase 2: Extraction ───────────────────────────────────────
        extractor = DualExtractor(self.filter_config, log_callback=self.log_callback)
        out_gen = OutputGenerator(self.output_folder)
        
        statuses: Dict[str, int] = {'SUCCESS': 0, 'FALLBACK': 0, 'SKIPPED': 0, 'ERROR': 0}
        changes_summary: Dict[str, Dict[str, int]] = {}
        errors: List[str] = []
        
        for i, file_info in enumerate(files_to_process):
            # Check pause/cancel
            self.pause_event.wait()
            if self.cancel_event.is_set():
                self._log("Pipeline cancelled by user.")
                break
                
            if self.file_started_callback:
                self.file_started_callback(file_info.relative_path)
                
            try:
                # Handle potentially locked file
                safe_path = self._handle_locked_file(file_info.absolute_path)
                
                # Temporarily update absolute_path for extraction
                original_path = file_info.absolute_path
                file_info.absolute_path = safe_path
                
                ext_result = extractor.extract(file_info)
                
                # Restore original path
                file_info.absolute_path = original_path
                
                # Cleanup temp file if it was created
                if safe_path != original_path:
                    try:
                        os.remove(safe_path)
                    except Exception as e:
                        self._log(f"Warning: Failed to clean up temp file {safe_path}: {e}")
                
                statuses[ext_result.status] = statuses.get(ext_result.status, 0) + 1
                
                if ext_result.error:
                    errors.append(f"{file_info.relative_path}: {ext_result.error}")
                
                # Diffing
                if ext_result.status in ('SUCCESS', 'FALLBACK'):
                    # Fetch previous records from DB for diff comparison
                    db_records = []
                    if self.db:
                        try:
                            from datetime import datetime, timezone
                            from db.models import ElementParameterRecord, ChangeLogRecord
                            
                            old_params = self.db.get_parameters_for_file(file_info.relative_path)
                            # Convert DB records to diff engine format
                            old_by_uid = {}
                            for p in old_params:
                                if p.unique_id not in old_by_uid:
                                    old_by_uid[p.unique_id] = {
                                        'unique_id': p.unique_id,
                                        'category': p.category,
                                        'parameters': {}
                                    }
                                old_by_uid[p.unique_id]['parameters'][p.parameter_name] = p.parameter_value
                            db_records = list(old_by_uid.values())
                        except Exception as e:
                            self._log(f"Warning: Could not fetch DB records: {e}")
                        
                    diff_report = DiffEngine.compare(file_info.relative_path, ext_result, db_records)
                    
                    changes_summary[file_info.relative_path] = {
                        'added': len(diff_report.added),
                        'modified': len(diff_report.modified),
                        'deleted': len(diff_report.deleted)
                    }
                    
                    out_gen.generate_file_reports(file_info.relative_path, diff_report, ext_result)
                    
                    # Update database with new parameter values
                    if self.db:
                        try:
                            now = datetime.now(timezone.utc).isoformat()
                            
                            # Upsert all current parameters
                            param_records = []
                            for el in ext_result.elements:
                                for pname, pval in el.parameters.items():
                                    param_records.append(ElementParameterRecord(
                                        relative_file_path=file_info.relative_path,
                                        unique_id=el.unique_id,
                                        category=el.category,
                                        element_name=el.element_name,
                                        parameter_name=pname,
                                        parameter_value=str(pval) if pval else '',
                                        last_updated=now
                                    ))
                            if param_records:
                                self.db.batch_upsert_parameters(param_records)
                            
                            # Delete parameters for removed elements
                            for rec in diff_report.deleted:
                                self.db.delete_parameters_for_element(
                                    file_info.relative_path, rec.unique_id
                                )
                            
                            # Log all changes
                            change_records = []
                            for rec in diff_report.added + diff_report.modified + diff_report.deleted:
                                change_records.append(ChangeLogRecord(
                                    relative_file_path=rec.relative_file_path,
                                    unique_id=rec.unique_id,
                                    category=rec.category,
                                    change_type=rec.change_type,
                                    parameter_name=rec.parameter_name,
                                    old_value=rec.old_value,
                                    new_value=rec.new_value,
                                    detected_at=now
                                ))
                            if change_records:
                                self.db.batch_add_change_logs(0, change_records)
                                
                        except Exception as e:
                            self._log(f"Warning: DB update failed: {e}")
                        
                if self.file_completed_callback:
                    self.file_completed_callback(file_info.relative_path, ext_result.status)
                    
            except Exception as e:
                self._log(f"Unhandled error processing {file_info.relative_path}: {e}")
                statuses['ERROR'] = statuses.get('ERROR', 0) + 1
                errors.append(f"{file_info.relative_path}: {e}")
                if self.file_completed_callback:
                    self.file_completed_callback(file_info.relative_path, 'ERROR')
                    
            finally:
                # Force memory cleanup
                gc.collect()
                
            if self.progress_callback:
                self.progress_callback(i + 1, total_files)
                
        out_gen.generate_executive_summary(total_files, statuses, changes_summary, errors)
        self._log("Pipeline finished.")

    def pause(self):
        self._log("Pausing pipeline...")
        self.pause_event.clear()
        
    def resume(self):
        self._log("Resuming pipeline...")
        self.pause_event.set()
        
    def cancel(self):
        self._log("Cancelling pipeline...")
        self.cancel_event.set()
        self.pause_event.set() # Unblock if paused

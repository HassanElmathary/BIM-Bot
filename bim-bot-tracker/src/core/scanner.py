import os
import logging
from typing import Dict, List, Optional
from .models import FileInfo, ScanResult
from .cloud_files import get_cloud_state, CLOUD_STATE_ONLINE_ONLY

logger = logging.getLogger(__name__)

class Scanner:
    """Recursively scans a directory for Revit (.rvt) and IFC (.ifc) files.
    
    Handles cloud-synced files from Autodesk Desktop Connector, OneDrive, etc.
    Detects whether each file is locally available or cloud-only (needs download).
    """
    
    def __init__(self, root_folder: str):
        self.root_folder = os.path.abspath(root_folder)
        # Handle long paths on Windows
        if os.name == 'nt' and not self.root_folder.startswith('\\\\?\\'):
            self.root_folder = '\\\\?\\' + self.root_folder

    def _is_backup_or_hidden(self, filename: str) -> bool:
        """Check if file is a lock file, backup file, or hidden file."""
        if filename.startswith('~') or filename.startswith('.'):
            return True
        # Check for backup pattern: name.0001.rvt
        parts = filename.split('.')
        if len(parts) >= 3 and parts[-2].isdigit() and len(parts[-2]) == 4:
            return True
        return False

    def scan(self) -> ScanResult:
        """Perform the directory scan.
        
        Returns a ScanResult with all .rvt and .ifc files found,
        including their cloud sync state.
        """
        logger.info(f"Starting scan in {self.root_folder}")
        all_files: List[FileInfo] = []
        dir_tree: Dict = {}
        total_rvt = 0
        total_ifc = 0
        cloud_count = 0
        
        # Temp dictionary to hold files in each dir for pairing
        dir_contents: Dict[str, Dict[str, list]] = {}
        
        try:
            for root, dirs, files in os.walk(self.root_folder):
                # Build tree logic
                rel_dir = os.path.relpath(root, self.root_folder)
                if rel_dir == '.':
                    current_node = dir_tree
                else:
                    parts = rel_dir.split(os.sep)
                    current_node = dir_tree
                    for part in parts:
                        if part not in current_node:
                            current_node[part] = {}
                        current_node = current_node[part]
                        
                dir_contents[root] = {'rvt': [], 'ifc': []}
                
                for f in files:
                    if self._is_backup_or_hidden(f):
                        continue
                    
                    lower_f = f.lower()
                    abs_path = os.path.join(root, f)
                    
                    # Get file stats — for cloud placeholders, st_size may
                    # report the *remote* file size (accurate on Desktop Connector)
                    try:
                        stat = os.stat(abs_path)
                    except OSError as e:
                        logger.warning(f"Failed to stat {abs_path}: {e}")
                        continue
                    
                    # Detect cloud sync state
                    cloud_state = get_cloud_state(abs_path)
                    is_cloud = (cloud_state == CLOUD_STATE_ONLINE_ONLY)
                    if is_cloud:
                        cloud_count += 1
                        
                    rel_path = os.path.relpath(abs_path, self.root_folder)
                    file_info = FileInfo(
                        relative_path=rel_path,
                        absolute_path=abs_path,
                        file_type='',
                        file_size=stat.st_size,
                        modified_time=stat.st_mtime,
                        cloud_state=cloud_state
                    )
                    
                    if lower_f.endswith('.rvt'):
                        file_info.file_type = 'rvt'
                        total_rvt += 1
                        all_files.append(file_info)
                        dir_contents[root]['rvt'].append(file_info)
                    elif lower_f.endswith('.ifc'):
                        file_info.file_type = 'ifc'
                        total_ifc += 1
                        all_files.append(file_info)
                        dir_contents[root]['ifc'].append(file_info)
                        
        except Exception as e:
            logger.error(f"Error during scan: {e}")
            
        # Pair RVT with IFC
        for rvt_file in all_files:
            if rvt_file.file_type == 'rvt':
                root_dir = os.path.dirname(rvt_file.absolute_path)
                base_name = os.path.splitext(os.path.basename(rvt_file.absolute_path))[0]
                
                # Look for matching ifc
                for ifc_file in dir_contents.get(root_dir, {}).get('ifc', []):
                    ifc_base_name = os.path.splitext(os.path.basename(ifc_file.absolute_path))[0]
                    if ifc_base_name.lower() == base_name.lower():
                        rvt_file.paired_ifc = ifc_file.absolute_path
                        break
        
        if cloud_count > 0:
            logger.info(
                f"Scan complete. Found {total_rvt} RVT + {total_ifc} IFC files. "
                f"⚠ {cloud_count} files are cloud-only and need downloading."
            )
        else:
            logger.info(f"Scan complete. Found {total_rvt} RVT files and {total_ifc} IFC files.")
        
        return ScanResult(
            root_folder=self.root_folder,
            files=all_files,
            directory_tree=dir_tree,
            total_rvt=total_rvt,
            total_ifc=total_ifc
        )

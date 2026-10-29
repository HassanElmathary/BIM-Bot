"""Cloud file handler for Autodesk Desktop Connector / OneDrive / cloud-synced files.

Detects cloud-only placeholder files (not yet downloaded) and triggers
hydration (download) before BIM-BOT processes them.

Works with:
- Autodesk Desktop Connector (ACC/BIM360/Forma)
- OneDrive
- Any Windows Cloud Files API provider

Cloud file states (Windows):
  - ONLINE-ONLY:  File is a placeholder stub, data is in the cloud
  - LOCALLY AVAILABLE: File has been downloaded/cached locally
  - ALWAYS AVAILABLE:  File is pinned to always stay local
"""

import ctypes
import ctypes.wintypes
import logging
import os
import time
from pathlib import Path
from typing import Callable, List, Optional, Tuple

logger = logging.getLogger(__name__)

# Windows file attribute flags for Cloud Files API
FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS = 0x00400000
FILE_ATTRIBUTE_RECALL_ON_OPEN = 0x00040000
FILE_ATTRIBUTE_OFFLINE = 0x00001000
FILE_ATTRIBUTE_PINNED = 0x00080000
FILE_ATTRIBUTE_UNPINNED = 0x00100000

# Cloud file states
CLOUD_STATE_LOCAL = "local"           # Fully downloaded
CLOUD_STATE_ONLINE_ONLY = "online"    # Cloud placeholder, needs download
CLOUD_STATE_PARTIAL = "partial"       # Partially downloaded
CLOUD_STATE_UNKNOWN = "unknown"       # Cannot determine


def get_file_attributes(file_path: str) -> int:
    """Get Win32 file attributes for a file path."""
    try:
        kernel32 = ctypes.windll.kernel32
        attrs = kernel32.GetFileAttributesW(str(file_path))
        if attrs == 0xFFFFFFFF:  # INVALID_FILE_ATTRIBUTES
            return 0
        return attrs
    except Exception:
        return 0


def get_cloud_state(file_path: str) -> str:
    """Determine the cloud sync state of a file.
    
    Returns one of: 'local', 'online', 'partial', 'unknown'
    
    Autodesk Desktop Connector cloud files:
      - Cloud-only:  0x00500020 (ARCHIVE + UNPINNED + RECALL_ON_DATA_ACCESS)
      - Downloaded:  0x00100020 (ARCHIVE + UNPINNED, no RECALL flags)
    """
    if os.name != 'nt':
        return CLOUD_STATE_LOCAL  # Non-Windows: assume local
    
    try:
        attrs = get_file_attributes(file_path)
        if attrs == 0:
            return CLOUD_STATE_UNKNOWN
        
        # Check for cloud placeholder flags
        is_recall_on_access = bool(attrs & FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS)
        is_recall_on_open = bool(attrs & FILE_ATTRIBUTE_RECALL_ON_OPEN)
        is_offline = bool(attrs & FILE_ATTRIBUTE_OFFLINE)
        
        if is_recall_on_access or is_recall_on_open or is_offline:
            return CLOUD_STATE_ONLINE_ONLY
        
        return CLOUD_STATE_LOCAL
        
    except Exception as e:
        logger.debug(f"Could not determine cloud state for {file_path}: {e}")
        return CLOUD_STATE_UNKNOWN


def is_cloud_placeholder(file_path: str) -> bool:
    """Check if a file is a cloud-only placeholder (not downloaded)."""
    state = get_cloud_state(file_path)
    return state == CLOUD_STATE_ONLINE_ONLY


def trigger_download(file_path: str) -> bool:
    """Trigger download of a cloud-only file by reading a small portion.
    
    This works because Windows Cloud Files API automatically hydrates
    (downloads) a file when it's accessed for reading.
    
    Returns True if the read succeeded (download triggered).
    """
    try:
        # Reading even 1 byte triggers the cloud provider to hydrate the file
        with open(file_path, 'rb') as f:
            f.read(1)
        return True
    except PermissionError:
        logger.warning(f"Permission denied when trying to download: {file_path}")
        return False
    except OSError as e:
        logger.warning(f"OS error triggering download for {file_path}: {e}")
        return False
    except Exception as e:
        logger.warning(f"Error triggering download for {file_path}: {e}")
        return False


def wait_for_download(file_path: str, timeout_seconds: int = 600,
                      poll_interval: float = 2.0,
                      progress_callback: Optional[Callable[[str, str], None]] = None) -> bool:
    """Wait for a cloud file to finish downloading.
    
    Args:
        file_path: Path to the file
        timeout_seconds: Max seconds to wait (default 10 min for large .rvt files)
        poll_interval: Seconds between state checks
        progress_callback: Called with (filename, status_message) during wait
        
    Returns True if file becomes locally available, False on timeout.
    """
    filename = os.path.basename(file_path)
    start_time = time.time()
    last_size = 0
    
    while time.time() - start_time < timeout_seconds:
        state = get_cloud_state(file_path)
        
        if state == CLOUD_STATE_LOCAL:
            if progress_callback:
                progress_callback(filename, "Downloaded")
            return True
        
        # Check file size for progress indication
        try:
            current_size = os.path.getsize(file_path)
            if current_size != last_size:
                last_size = current_size
                size_mb = current_size / (1024 * 1024)
                elapsed = int(time.time() - start_time)
                if progress_callback:
                    progress_callback(filename, f"Downloading... {size_mb:.1f} MB ({elapsed}s)")
        except Exception:
            pass
        
        time.sleep(poll_interval)
    
    logger.warning(f"Download timed out for {file_path} after {timeout_seconds}s")
    if progress_callback:
        progress_callback(filename, "Download timed out!")
    return False


def ensure_files_downloaded(
    file_paths: List[str],
    log_callback: Optional[Callable[[str], None]] = None,
    progress_callback: Optional[Callable[[int, int], None]] = None,
    file_progress_callback: Optional[Callable[[str, str], None]] = None,
    cancel_check: Optional[Callable[[], bool]] = None,
    timeout_per_file: int = 600
) -> Tuple[List[str], List[str]]:
    """Ensure all files in the list are downloaded locally.
    
    For each cloud-only file:
    1. Triggers download by reading from the file
    2. Waits for the download to complete
    
    Args:
        file_paths: List of absolute file paths to check
        log_callback: Called with log messages
        progress_callback: Called with (current, total) download progress
        file_progress_callback: Called with (filename, status) per file
        cancel_check: Returns True if operation should be cancelled
        timeout_per_file: Max seconds to wait per file
        
    Returns:
        Tuple of (downloaded_paths, failed_paths)
    """
    def _log(msg):
        logger.info(msg)
        if log_callback:
            log_callback(msg)
    
    cloud_files = []
    local_files = []
    
    # Phase 1: Classify files
    for fp in file_paths:
        if is_cloud_placeholder(fp):
            cloud_files.append(fp)
        else:
            local_files.append(fp)
    
    if not cloud_files:
        _log("All files are locally available.")
        return file_paths, []
    
    _log(f"Found {len(cloud_files)} cloud-only files that need downloading.")
    _log(f"{len(local_files)} files already available locally.")
    
    downloaded = list(local_files)
    failed = []
    
    # Phase 2: Trigger downloads for all cloud files first
    _log("Triggering downloads for all cloud files...")
    for fp in cloud_files:
        if cancel_check and cancel_check():
            _log("Download cancelled by user.")
            return downloaded, cloud_files
        
        filename = os.path.basename(fp)
        _log(f"📥 Requesting download: {filename}")
        trigger_download(fp)
    
    # Phase 3: Wait for all downloads to complete
    _log("Waiting for downloads to complete...")
    total = len(cloud_files)
    
    for i, fp in enumerate(cloud_files):
        if cancel_check and cancel_check():
            _log("Download cancelled by user.")
            failed.extend(cloud_files[i:])
            return downloaded, failed
        
        filename = os.path.basename(fp)
        
        if progress_callback:
            progress_callback(i + 1, total)
        
        if file_progress_callback:
            file_progress_callback(filename, "Downloading...")
        
        success = wait_for_download(
            fp,
            timeout_seconds=timeout_per_file,
            progress_callback=file_progress_callback
        )
        
        if success:
            _log(f"✅ Downloaded: {filename}")
            downloaded.append(fp)
        else:
            _log(f"❌ Failed to download: {filename}")
            failed.append(fp)
    
    _log(f"Download phase complete: {len(downloaded)} ready, {len(failed)} failed.")
    return downloaded, failed

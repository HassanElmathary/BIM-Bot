"""Database record data classes for BIM-BOT."""

from dataclasses import dataclass
from typing import Optional

@dataclass
class ScanSessionRecord:
    id: Optional[int] = None
    root_folder: str = ''
    scan_start: str = ''     # ISO format timestamp
    scan_end: Optional[str] = None
    total_files: int = 0
    files_succeeded: int = 0
    files_skipped: int = 0
    status: str = 'IN_PROGRESS'  # IN_PROGRESS, COMPLETED, CANCELLED, ERROR

@dataclass
class ModelSnapshotRecord:
    id: Optional[int] = None
    session_id: Optional[int] = None
    relative_file_path: str = ''
    file_size: int = 0
    file_modified: Optional[str] = None
    revit_version: Optional[str] = None
    extraction_engine: Optional[str] = None   # 'revit-extractor', 'ifcopenshell', 'skipped'
    scan_timestamp: str = ''
    element_count: int = 0

@dataclass
class ElementParameterRecord:
    id: Optional[int] = None
    relative_file_path: str = ''
    unique_id: str = ''
    category: str = ''
    element_name: str = ''
    parameter_name: str = ''
    parameter_value: str = ''
    last_updated: str = ''

@dataclass
class ChangeLogRecord:
    id: Optional[int] = None
    session_id: Optional[int] = None
    relative_file_path: str = ''
    unique_id: str = ''
    category: str = ''
    change_type: str = ''     # 'ADDED', 'MODIFIED', 'DELETED'
    parameter_name: str = ''
    old_value: str = ''
    new_value: str = ''
    detected_at: str = ''

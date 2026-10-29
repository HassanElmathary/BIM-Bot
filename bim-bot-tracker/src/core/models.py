from dataclasses import dataclass, field
from typing import List, Dict, Optional

@dataclass
class FileInfo:
    relative_path: str
    absolute_path: str
    file_type: str
    file_size: int
    modified_time: float
    paired_ifc: Optional[str] = None
    cloud_state: str = 'local'  # 'local', 'online', 'partial', 'unknown'

@dataclass
class FileMetadata:
    revit_version: Optional[str] = None
    build_number: Optional[str] = None
    project_name: Optional[str] = None
    file_size: int = 0
    modified_time: float = 0.0

@dataclass
class ElementData:
    unique_id: str
    category: str
    element_name: str = ''
    parameters: Dict[str, str] = field(default_factory=dict)

@dataclass
class ExtractionResult:
    status: str
    engine_used: str
    elements: List[ElementData]
    element_count: int = 0
    error: Optional[str] = None
    warnings: List[str] = field(default_factory=list)

@dataclass
class DiffRecord:
    relative_file_path: str
    unique_id: str
    category: str
    change_type: str
    parameter_name: str = ''
    old_value: str = ''
    new_value: str = ''

@dataclass
class DiffReport:
    relative_file_path: str
    added: List[DiffRecord]
    modified: List[DiffRecord]
    deleted: List[DiffRecord]
    unchanged_count: int = 0

@dataclass
class ScanResult:
    root_folder: str
    files: List[FileInfo]
    directory_tree: dict
    total_rvt: int = 0
    total_ifc: int = 0

class FilterConfig:
    DEFAULT_CATEGORIES = [
        "Walls", "Doors", "Windows", "Structural Framing",
        "Structural Columns", "Floors", "Roofs", "Ceilings",
        "Mechanical Equipment", "Plumbing Fixtures", "Electrical Equipment",
        "Pipes", "Ducts"
    ]
    DEFAULT_PARAMETERS = [
        "UniqueId", "Mark", "Type", "Comments", "Phase Created",
        "Fire Rating", "Keynote", "Level", "Area", "Volume",
        "Width", "Height", "Length"
    ]
    
    def __init__(self, categories: Optional[List[str]] = None, parameters: Optional[List[str]] = None):
        self.categories = categories if categories is not None else self.DEFAULT_CATEGORIES.copy()
        self.parameters = parameters if parameters is not None else self.DEFAULT_PARAMETERS.copy()

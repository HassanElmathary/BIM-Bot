import logging
from typing import Callable, Optional
from .models import FileInfo, ExtractionResult, FilterConfig
from .rvt_extractor import RvtExtractor
from .ifc_extractor import IfcExtractor

logger = logging.getLogger(__name__)

class DualExtractor:
    """Orchestrates extraction, using Revit extractor or falling back to IFC."""
    
    def __init__(self, filter_config: FilterConfig, log_callback: Optional[Callable[[str], None]] = None):
        self.filter_config = filter_config
        self.rvt_extractor = RvtExtractor(filter_config)
        self.ifc_extractor = IfcExtractor(filter_config)
        self.log_callback = log_callback

    def _log(self, message: str):
        logger.info(message)
        if self.log_callback:
            self.log_callback(message)

    def extract(self, file_info: FileInfo) -> ExtractionResult:
        if file_info.file_type == 'rvt':
            self._log(f"Attempting RVT extraction for {file_info.relative_path}")
            result = self.rvt_extractor.extract(file_info.absolute_path)
            
            if result.status == 'SUCCESS':
                return result
                
            self._log(f"RVT extraction failed for {file_info.relative_path}. Checking for IFC fallback...")
            
            if file_info.paired_ifc:
                self._log(f"Found paired IFC: {file_info.paired_ifc}. Attempting IFC extraction...")
                ifc_result = self.ifc_extractor.extract(file_info.paired_ifc)
                ifc_result.status = 'FALLBACK' if ifc_result.status == 'SUCCESS' else ifc_result.status
                return ifc_result
            else:
                self._log(f"No paired IFC found for {file_info.relative_path}.")
                return result
                
        elif file_info.file_type == 'ifc':
            self._log(f"Attempting IFC extraction for {file_info.relative_path}")
            return self.ifc_extractor.extract(file_info.absolute_path)
            
        else:
            self._log(f"Unknown file type for {file_info.relative_path}")
            return ExtractionResult(status='SKIPPED', engine_used='none', elements=[], error="Unknown file type")

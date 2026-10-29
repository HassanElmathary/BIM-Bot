import logging
from typing import List, Dict
from .models import ExtractionResult, ElementData, DiffReport, DiffRecord

logger = logging.getLogger(__name__)

class DiffEngine:
    """Compares current extraction results with previous (database) records."""
    
    @staticmethod
    def compare(relative_file_path: str, current_result: ExtractionResult, db_records: List[Dict]) -> DiffReport:
        """
        db_records is expected to be a list of dicts:
        {
            'unique_id': str,
            'category': str,
            'parameters': Dict[str, str]
        }
        """
        report = DiffReport(relative_file_path=relative_file_path, added=[], modified=[], deleted=[])
        
        # Build lookup for old records
        old_map = {rec['unique_id']: rec for rec in db_records}
        
        # Build lookup for new records
        new_map = {el.unique_id: el for el in current_result.elements}
        
        # Check for added and modified
        for unique_id, new_el in new_map.items():
            if unique_id not in old_map:
                # ADDED
                for param_name, param_value in new_el.parameters.items():
                    report.added.append(DiffRecord(
                        relative_file_path=relative_file_path,
                        unique_id=unique_id,
                        category=new_el.category,
                        change_type='ADDED',
                        parameter_name=param_name,
                        new_value=param_value
                    ))
            else:
                # Exists in both, check for modifications
                old_rec = old_map[unique_id]
                old_params = old_rec.get('parameters', {})
                
                # We check parameters tracked in the new extraction
                for param_name, new_val in new_el.parameters.items():
                    old_val = old_params.get(param_name, '')
                    if old_val != new_val:
                        report.modified.append(DiffRecord(
                            relative_file_path=relative_file_path,
                            unique_id=unique_id,
                            category=new_el.category,
                            change_type='MODIFIED',
                            parameter_name=param_name,
                            old_value=old_val,
                            new_value=new_val
                        ))
                    else:
                        report.unchanged_count += 1
                        
        # Check for deleted
        for unique_id, old_rec in old_map.items():
            if unique_id not in new_map:
                # DELETED
                for param_name, old_val in old_rec.get('parameters', {}).items():
                    report.deleted.append(DiffRecord(
                        relative_file_path=relative_file_path,
                        unique_id=unique_id,
                        category=old_rec.get('category', ''),
                        change_type='DELETED',
                        parameter_name=param_name,
                        old_value=old_val
                    ))
                    
        return report

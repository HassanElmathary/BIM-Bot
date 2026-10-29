import os
import csv
import json
import logging
from typing import List, Dict
from .models import DiffReport, ExtractionResult

logger = logging.getLogger(__name__)

class OutputGenerator:
    """Generates report files (CSV, JSON, Markdown)."""
    
    def __init__(self, output_dir: str):
        self.output_dir = os.path.abspath(output_dir)
        os.makedirs(self.output_dir, exist_ok=True)

    def generate_file_reports(self, relative_file_path: str, diff_report: DiffReport, extraction_result: ExtractionResult):
        """Generates changes.csv, changes.json, and snapshot.csv for a specific file."""
        # Create mirror directory structure
        file_dir = os.path.dirname(relative_file_path)
        base_name = os.path.splitext(os.path.basename(relative_file_path))[0]
        
        target_dir = os.path.join(self.output_dir, file_dir)
        os.makedirs(target_dir, exist_ok=True)
        
        changes_csv = os.path.join(target_dir, f"{base_name}_changes.csv")
        changes_json = os.path.join(target_dir, f"{base_name}_changes.json")
        snapshot_csv = os.path.join(target_dir, f"{base_name}_snapshot.csv")
        
        # 1. Write changes.csv
        all_changes = diff_report.added + diff_report.modified + diff_report.deleted
        try:
            with open(changes_csv, 'w', newline='', encoding='utf-8') as f:
                writer = csv.writer(f)
                writer.writerow(['UniqueId', 'Category', 'ChangeType', 'Parameter', 'OldValue', 'NewValue'])
                for rec in all_changes:
                    writer.writerow([rec.unique_id, rec.category, rec.change_type, rec.parameter_name, rec.old_value, rec.new_value])
        except Exception as e:
            logger.error(f"Failed to write {changes_csv}: {e}")
            
        # 2. Write changes.json
        try:
            json_data = {
                "file": relative_file_path,
                "added": [vars(r) for r in diff_report.added],
                "modified": [vars(r) for r in diff_report.modified],
                "deleted": [vars(r) for r in diff_report.deleted]
            }
            with open(changes_json, 'w', encoding='utf-8') as f:
                json.dump(json_data, f, indent=2)
        except Exception as e:
            logger.error(f"Failed to write {changes_json}: {e}")
            
        # 3. Write snapshot.csv
        try:
            with open(snapshot_csv, 'w', newline='', encoding='utf-8') as f:
                writer = csv.writer(f)
                # We need all parameter names to form columns
                all_params = set()
                for el in extraction_result.elements:
                    all_params.update(el.parameters.keys())
                
                header = ['UniqueId', 'Category'] + sorted(list(all_params))
                writer.writerow(header)
                
                for el in extraction_result.elements:
                    row = [el.unique_id, el.category]
                    for p in sorted(list(all_params)):
                        row.append(el.parameters.get(p, ''))
                    writer.writerow(row)
        except Exception as e:
            logger.error(f"Failed to write {snapshot_csv}: {e}")

    def generate_executive_summary(self, total_files: int, statuses: Dict[str, int], changes_summary: Dict[str, Dict[str, int]], errors: List[str]):
        """Generates executive_summary.md"""
        summary_path = os.path.join(self.output_dir, "executive_summary.md")
        try:
            with open(summary_path, 'w', encoding='utf-8') as f:
                f.write("# Executive Summary\n\n")
                f.write(f"**Total Files Processed:** {total_files}\n\n")
                
                f.write("## Status Breakdown\n")
                f.write("| Status | Count |\n")
                f.write("|---|---|\n")
                for status, count in statuses.items():
                    f.write(f"| {status} | {count} |\n")
                f.write("\n")
                
                f.write("## Changes by File\n")
                f.write("| File | Added | Modified | Deleted |\n")
                f.write("|---|---|---|---|\n")
                for file_path, counts in changes_summary.items():
                    f.write(f"| {file_path} | {counts.get('added', 0)} | {counts.get('modified', 0)} | {counts.get('deleted', 0)} |\n")
                f.write("\n")
                
                if errors:
                    f.write("## Errors\n")
                    for err in errors:
                        f.write(f"- {err}\n")
                        
        except Exception as e:
            logger.error(f"Failed to write {summary_path}: {e}")

    def write_scan_log(self, log_content: str):
        """Generates scan_log.txt"""
        log_path = os.path.join(self.output_dir, "scan_log.txt")
        try:
            with open(log_path, 'a', encoding='utf-8') as f:
                f.write(log_content + "\n")
        except Exception as e:
            logger.error(f"Failed to write {log_path}: {e}")

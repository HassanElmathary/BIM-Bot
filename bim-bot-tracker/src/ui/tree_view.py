"""Directory tree view."""
import os
from dataclasses import dataclass
from typing import List, Dict, Any
from PySide6.QtWidgets import QTreeView, QHeaderView
from PySide6.QtGui import QStandardItemModel, QStandardItem, QColor
from PySide6.QtCore import Qt

from ui.styles import SUCCESS, ERROR, WARNING, TEXT_PRIMARY

@dataclass
class ScanResult:
    files: List[str]

class FileTreeView(QTreeView):
    """Tree view for displaying scanned directory structure."""
    
    def __init__(self, parent=None):
        super().__init__(parent)
        self.model = QStandardItemModel()
        self.model.setHorizontalHeaderLabels([
            "Name", "Type", "Size", "Revit Version", "IFC Paired", "Status"
        ])
        self.setModel(self.model)
        self.setAlternatingRowColors(True)
        self.setSortingEnabled(True)
        
        header = self.header()
        header.setSectionResizeMode(0, QHeaderView.ResizeMode.Stretch)
        header.setSectionResizeMode(1, QHeaderView.ResizeMode.ResizeToContents)
        header.setSectionResizeMode(2, QHeaderView.ResizeMode.ResizeToContents)
        header.setSectionResizeMode(3, QHeaderView.ResizeMode.ResizeToContents)
        header.setSectionResizeMode(4, QHeaderView.ResizeMode.ResizeToContents)
        header.setSectionResizeMode(5, QHeaderView.ResizeMode.ResizeToContents)
        
        self._item_map = {}

    def populate(self, scan_result: ScanResult, metadata: Dict[str, Any]):
        self.model.removeRows(0, self.model.rowCount())
        self._item_map.clear()
        
        root_node = self.model.invisibleRootItem()
        
        # Build tree structure
        for file_path in sorted(scan_result.files):
            parts = file_path.replace('\\', '/').split('/')
            current_node = root_node
            
            for i, part in enumerate(parts):
                is_file = (i == len(parts) - 1)
                item_id = '/'.join(parts[:i+1])
                
                if item_id not in self._item_map:
                    item = QStandardItem(part)
                    item.setEditable(False)
                    
                    if is_file:
                        ext = os.path.splitext(part)[1].lower()
                        type_item = QStandardItem(ext)
                        size_item = QStandardItem("") # Add size logic if available
                        
                        file_meta = metadata.get(file_path, {})
                        revit_ver = str(file_meta.get("revit_version", ""))
                        ifc_paired = "paired ✓" if file_meta.get("has_ifc") else ""
                        
                        version_item = QStandardItem(revit_ver)
                        ifc_item = QStandardItem(ifc_paired)
                        status_item = QStandardItem("pending")
                        
                        current_node.appendRow([item, type_item, size_item, version_item, ifc_item, status_item])
                        self._item_map[file_path] = status_item
                    else:
                        type_item = QStandardItem("folder")
                        current_node.appendRow([item, type_item, QStandardItem(""), QStandardItem(""), QStandardItem(""), QStandardItem("")])
                        self._item_map[item_id] = item
                        current_node = item
                else:
                    if not is_file:
                        current_node = self._item_map[item_id]

        self.expandAll()

    def update_file_status(self, relative_path: str, status: str):
        if relative_path in self._item_map:
            status_item = self._item_map[relative_path]
            
            icons = {
                "success": "✅",
                "fallback": "↩️",
                "skipped": "❌",
                "error": "❌",
                "pending": "⏳",
                "processing": "⚙️",
                "cloud": "☁️",
                "downloading": "📥",
                "downloaded": "💾",
            }
            
            icon = icons.get(status, "")
            status_item.setText(f"{status} {icon}")
            
            # Update row color
            row = status_item.row()
            parent = status_item.parent() or self.model.invisibleRootItem()
            
            color_map = {
                "success": QColor(SUCCESS),
                "error": QColor(ERROR),
                "skipped": QColor(ERROR),
                "fallback": QColor(WARNING),
                "pending": QColor(TEXT_PRIMARY)
            }
            
            color = color_map.get(status, QColor(TEXT_PRIMARY))
            
            for col in range(self.model.columnCount()):
                item = parent.child(row, col)
                if item:
                    item.setForeground(color)

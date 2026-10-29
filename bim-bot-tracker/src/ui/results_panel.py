"""Results and Parameter Audit panel for BIM-BOT."""
import csv
import logging
from dataclasses import dataclass
from typing import List, Dict, Any, Optional

from PySide6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QLabel, QTableView, QHeaderView,
    QPushButton, QLineEdit, QComboBox, QFileDialog, QTabWidget, QGroupBox,
    QFrame
)
from PySide6.QtGui import QStandardItemModel, QStandardItem, QColor, QFont
from PySide6.QtCore import Qt, Signal

from ui.styles import (
    SUCCESS, WARNING, ERROR, ACCENT, BG_PRIMARY, BG_SECONDARY, TEXT_PRIMARY, TEXT_MUTED
)
from db.models import ElementParameterRecord, ChangeLogRecord

logger = logging.getLogger(__name__)


@dataclass
class DiffReportRow:
    file_path: str
    element_id: str
    category: str
    change_type: str
    parameter: str
    old_value: Any
    new_value: Any


class ResultsPanel(QWidget):
    """Panel for parameter completeness audit and diff results."""

    def __init__(self, parent=None):
        super().__init__(parent)
        self.parameter_records: List[ElementParameterRecord] = []
        self.diff_records: List[DiffReportRow] = []
        self.filtered_param_records: List[ElementParameterRecord] = []

        self._setup_ui()

    def _setup_ui(self):
        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(4, 4, 4, 4)

        # Tab Widget: 1) Parameter Audit, 2) Diff Tracker
        self.tabs = QTabWidget()

        # Build Sub-Tabs
        self.audit_widget = QWidget()
        self.diff_widget = QWidget()

        self._setup_audit_tab(self.audit_widget)
        self._setup_diff_tab(self.diff_widget)

        self.tabs.addTab(self.audit_widget, "📋 Parameter Audit & Completeness")
        self.tabs.addTab(self.diff_widget, "🔄 Change Log / Diffs")

        main_layout.addWidget(self.tabs)

    # ------------------------------------------------------------------
    # 1. PARAMETER AUDIT TAB SETUP
    # ------------------------------------------------------------------
    def _setup_audit_tab(self, parent: QWidget):
        layout = QVBoxLayout(parent)
        layout.setContentsMargins(4, 4, 4, 4)

        # KPI Summary Cards Row
        kpi_layout = QHBoxLayout()

        self.lbl_total_params = self._create_card("Total Params: 0", ACCENT)
        self.lbl_filled_params = self._create_card("Filled: 0 (0%)", SUCCESS)
        self.lbl_empty_params = self._create_card("Empty / Unfilled: 0 (0%)", WARNING)
        self.lbl_showing = self._create_card("Showing: 0", TEXT_PRIMARY)

        kpi_layout.addWidget(self.lbl_total_params)
        kpi_layout.addWidget(self.lbl_filled_params)
        kpi_layout.addWidget(self.lbl_empty_params)
        kpi_layout.addWidget(self.lbl_showing)
        kpi_layout.addStretch()

        layout.addLayout(kpi_layout)

        # Filter & Search Control Panel
        filter_group = QGroupBox("Search & Parameter Filters")
        fg_layout = QVBoxLayout(filter_group)

        # Row 1: Search box + Match Operator + Target Field
        row1 = QHBoxLayout()

        lbl_search = QLabel("Search:")
        self.txt_audit_search = QLineEdit()
        self.txt_audit_search.setPlaceholderText("Type parameter name (e.g. Mark, Fire Rating) or value...")
        self.txt_audit_search.textChanged.connect(self._apply_audit_filters)

        lbl_match = QLabel("Match Mode:")
        self.cbo_match_mode = QComboBox()
        self.cbo_match_mode.addItems(["Contains", "Starts With", "Ends With", "Equals"])
        self.cbo_match_mode.currentIndexChanged.connect(self._apply_audit_filters)

        lbl_target = QLabel("Search In:")
        self.cbo_search_target = QComboBox()
        self.cbo_search_target.addItems([
            "All Fields", "Parameter Name", "Parameter Value", "Category", "File / Element ID"
        ])
        self.cbo_search_target.currentIndexChanged.connect(self._apply_audit_filters)

        row1.addWidget(lbl_search)
        row1.addWidget(self.txt_audit_search, stretch=2)
        row1.addWidget(lbl_match)
        row1.addWidget(self.cbo_match_mode)
        row1.addWidget(lbl_target)
        row1.addWidget(self.cbo_search_target)

        # Row 2: Parameter Selector + Status Filter + Category Filter + Export
        row2 = QHBoxLayout()

        lbl_pselect = QLabel("Parameter:")
        self.cbo_param_select = QComboBox()
        self.cbo_param_select.addItem("-- All Parameters --")
        self.cbo_param_select.currentIndexChanged.connect(self._apply_audit_filters)

        lbl_status = QLabel("Status:")
        self.cbo_status_filter = QComboBox()
        self.cbo_status_filter.addItems([
            "All Statuses", "⚠️ Empty / Unfilled Only", "✅ Filled Only"
        ])
        self.cbo_status_filter.currentIndexChanged.connect(self._apply_audit_filters)

        lbl_cat = QLabel("Category:")
        self.cbo_cat_filter = QComboBox()
        self.cbo_cat_filter.addItem("-- All Categories --")
        self.cbo_cat_filter.currentIndexChanged.connect(self._apply_audit_filters)

        btn_reset = QPushButton("🧹 Reset Filters")
        btn_reset.clicked.connect(self._reset_audit_filters)

        btn_export_audit = QPushButton("📊 Export Audit CSV")
        btn_export_audit.setStyleSheet(f"background-color: {ACCENT}; color: #000; font-weight: bold;")
        btn_export_audit.clicked.connect(self._export_audit_csv)

        row2.addWidget(lbl_pselect)
        row2.addWidget(self.cbo_param_select, stretch=1)
        row2.addWidget(lbl_status)
        row2.addWidget(self.cbo_status_filter)
        row2.addWidget(lbl_cat)
        row2.addWidget(self.cbo_cat_filter)
        row2.addWidget(btn_reset)
        row2.addWidget(btn_export_audit)

        fg_layout.addLayout(row1)
        fg_layout.addLayout(row2)

        layout.addWidget(filter_group)

        # Table View for Audit Results
        self.table_audit = QTableView()
        self.model_audit = QStandardItemModel()
        self.model_audit.setHorizontalHeaderLabels([
            "Status", "Parameter Name", "Parameter Value", "Category", "Element UniqueId", "File Path"
        ])
        self.table_audit.setModel(self.model_audit)
        self.table_audit.setSortingEnabled(True)
        self.table_audit.setAlternatingRowColors(True)

        header = self.table_audit.horizontalHeader()
        header.setSectionResizeMode(0, QHeaderView.ResizeMode.ResizeToContents)
        header.setSectionResizeMode(1, QHeaderView.ResizeMode.Interactive)
        header.setSectionResizeMode(2, QHeaderView.ResizeMode.Stretch)
        header.setSectionResizeMode(3, QHeaderView.ResizeMode.Interactive)
        header.setSectionResizeMode(4, QHeaderView.ResizeMode.Interactive)
        header.setSectionResizeMode(5, QHeaderView.ResizeMode.Interactive)

        layout.addWidget(self.table_audit)

    # ------------------------------------------------------------------
    # 2. DIFF TRACKER TAB SETUP
    # ------------------------------------------------------------------
    def _setup_diff_tab(self, parent: QWidget):
        layout = QVBoxLayout(parent)
        layout.setContentsMargins(4, 4, 4, 4)

        # Summary Cards
        summary_layout = QHBoxLayout()
        self.lbl_added = self._create_card("Added: 0", SUCCESS)
        self.lbl_mod = self._create_card("Modified: 0", WARNING)
        self.lbl_del = self._create_card("Deleted: 0", ERROR)
        summary_layout.addWidget(self.lbl_added)
        summary_layout.addWidget(self.lbl_mod)
        summary_layout.addWidget(self.lbl_del)
        summary_layout.addStretch()

        # Filter Box & Export
        top_layout = QHBoxLayout()
        self.txt_diff_search = QLineEdit()
        self.txt_diff_search.setPlaceholderText("Filter diff results...")
        self.txt_diff_search.textChanged.connect(self._filter_diff_results)

        btn_export_diff = QPushButton("📊 Export Diff CSV")
        btn_export_diff.clicked.connect(self._export_diff_csv)

        top_layout.addWidget(self.txt_diff_search)
        top_layout.addWidget(btn_export_diff)

        # Table
        self.table_diff = QTableView()
        self.model_diff = QStandardItemModel()
        self.model_diff.setHorizontalHeaderLabels([
            "File", "Element UniqueId", "Category", "Change Type",
            "Parameter", "Old Value", "New Value"
        ])
        self.table_diff.setModel(self.model_diff)
        self.table_diff.setSortingEnabled(True)
        self.table_diff.setAlternatingRowColors(True)
        self.table_diff.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Interactive)
        self.table_diff.horizontalHeader().setStretchLastSection(True)

        layout.addLayout(summary_layout)
        layout.addLayout(top_layout)
        layout.addWidget(self.table_diff)

    def _create_card(self, text: str, color: str) -> QLabel:
        lbl = QLabel(text)
        lbl.setStyleSheet(
            f"background-color: {BG_SECONDARY}; color: {color}; "
            f"padding: 6px 12px; border-radius: 4px; font-weight: bold; font-size: 10pt;"
        )
        return lbl

    # ------------------------------------------------------------------
    # DATA LOADING METHODS
    # ------------------------------------------------------------------
    def set_parameter_records(self, records: List[ElementParameterRecord]):
        """Load full element parameter list for audit view."""
        self.parameter_records = records

        # Update category and parameter dropdowns
        categories = sorted(list({r.category for r in records if r.category}))
        parameters = sorted(list({r.parameter_name for r in records if r.parameter_name}))

        self.cbo_cat_filter.blockSignals(True)
        self.cbo_cat_filter.clear()
        self.cbo_cat_filter.addItem("-- All Categories --")
        for cat in categories:
            self.cbo_cat_filter.addItem(cat)
        self.cbo_cat_filter.blockSignals(False)

        self.cbo_param_select.blockSignals(True)
        self.cbo_param_select.clear()
        self.cbo_param_select.addItem("-- All Parameters --")
        for p in parameters:
            self.cbo_param_select.addItem(p)
        self.cbo_param_select.blockSignals(False)

        self._apply_audit_filters()

    def set_diff_results(self, diff_reports: List[DiffReportRow]):
        """Load diff change records."""
        self.diff_records = diff_reports
        self.model_diff.removeRows(0, self.model_diff.rowCount())

        counts = {"Added": 0, "Modified": 0, "Deleted": 0}

        for report in diff_reports:
            counts[report.change_type] = counts.get(report.change_type, 0) + 1

            row = [
                QStandardItem(report.file_path),
                QStandardItem(report.element_id),
                QStandardItem(report.category),
                QStandardItem(report.change_type),
                QStandardItem(report.parameter),
                QStandardItem(str(report.old_value)),
                QStandardItem(str(report.new_value))
            ]

            for item in row:
                item.setEditable(False)

            change_item = row[3]
            if report.change_type == "Added":
                change_item.setForeground(QColor(SUCCESS))
            elif report.change_type == "Modified":
                change_item.setForeground(QColor(WARNING))
            elif report.change_type == "Deleted":
                change_item.setForeground(QColor(ERROR))

            self.model_diff.appendRow(row)

        self.lbl_added.setText(f"Added: {counts['Added']}")
        self.lbl_mod.setText(f"Modified: {counts['Modified']}")
        self.lbl_del.setText(f"Deleted: {counts['Deleted']}")

    # ------------------------------------------------------------------
    # AUDIT FILTERING & SEARCH ENGINE
    # ------------------------------------------------------------------
    def _reset_audit_filters(self):
        self.txt_audit_search.clear()
        self.cbo_match_mode.setCurrentIndex(0)  # Contains
        self.cbo_search_target.setCurrentIndex(0)  # All Fields
        self.cbo_param_select.setCurrentIndex(0)  # All Parameters
        self.cbo_status_filter.setCurrentIndex(0)  # All Statuses
        self.cbo_cat_filter.setCurrentIndex(0)  # All Categories
        self._apply_audit_filters()

    def _match_string(self, text: str, query: str, mode: str) -> bool:
        if not query:
            return True
        text = text.lower()
        query = query.lower()

        if mode == "Contains":
            return query in text
        elif mode == "Starts With":
            return text.startswith(query)
        elif mode == "Ends With":
            return text.endswith(query)
        elif mode == "Equals":
            return text == query
        return False

    def _apply_audit_filters(self):
        query = self.txt_audit_search.text().strip()
        match_mode = self.cbo_match_mode.currentText()
        target = self.cbo_search_target.currentText()

        selected_param = self.cbo_param_select.currentText()
        if selected_param.startswith("--"):
            selected_param = None

        selected_cat = self.cbo_cat_filter.currentText()
        if selected_cat.startswith("--"):
            selected_cat = None

        status_idx = self.cbo_status_filter.currentIndex()
        # 0: All, 1: Empty only, 2: Filled only

        self.filtered_param_records = []

        total_count = len(self.parameter_records)
        filled_count = 0
        empty_count = 0

        for r in self.parameter_records:
            val_str = (r.parameter_value or "").strip()
            is_empty = (val_str == "")

            if is_empty:
                empty_count += 1
            else:
                filled_count += 1

            # 1. Parameter Dropdown Filter
            if selected_param and r.parameter_name != selected_param:
                continue

            # 2. Category Dropdown Filter
            if selected_cat and r.category != selected_cat:
                continue

            # 3. Status Filter
            if status_idx == 1 and not is_empty:
                continue
            if status_idx == 2 and is_empty:
                continue

            # 4. Flexible Text Search Filter
            if query:
                pname = r.parameter_name or ""
                pval = r.parameter_value or ""
                cat = r.category or ""
                uid = r.unique_id or ""
                fpath = r.relative_file_path or ""

                if target == "Parameter Name":
                    if not self._match_string(pname, query, match_mode):
                        continue
                elif target == "Parameter Value":
                    if not self._match_string(pval, query, match_mode):
                        continue
                elif target == "Category":
                    if not self._match_string(cat, query, match_mode):
                        continue
                elif target == "File / Element ID":
                    if not self._match_string(fpath, query, match_mode) and not self._match_string(uid, query, match_mode):
                        continue
                else:  # All Fields
                    m1 = self._match_string(pname, query, match_mode)
                    m2 = self._match_string(pval, query, match_mode)
                    m3 = self._match_string(cat, query, match_mode)
                    m4 = self._match_string(uid, query, match_mode)
                    m5 = self._match_string(fpath, query, match_mode)
                    if not (m1 or m2 or m3 or m4 or m5):
                        continue

            self.filtered_param_records.append(r)

        # Update KPI Summary Cards
        if total_count > 0:
            filled_pct = (filled_count / total_count) * 100.0
            empty_pct = (empty_count / total_count) * 100.0
        else:
            filled_pct = 0.0
            empty_pct = 0.0

        self.lbl_total_params.setText(f"Total Params: {total_count:,}")
        self.lbl_filled_params.setText(f"Filled: {filled_count:,} ({filled_pct:.1f}%)")
        self.lbl_empty_params.setText(f"Empty / Unfilled: {empty_count:,} ({empty_pct:.1f}%)")
        self.lbl_showing.setText(f"Showing: {len(self.filtered_param_records):,}")

        # Populate Audit Table
        self.model_audit.removeRows(0, self.model_audit.rowCount())

        for r in self.filtered_param_records:
            val_str = (r.parameter_value or "").strip()
            is_empty = (val_str == "")

            status_str = "⚠️ EMPTY" if is_empty else "✅ FILLED"
            disp_val = "(EMPTY / UNFILLED)" if is_empty else val_str

            item_status = QStandardItem(status_str)
            item_pname = QStandardItem(r.parameter_name)
            item_pval = QStandardItem(disp_val)
            item_cat = QStandardItem(r.category)
            item_uid = QStandardItem(r.unique_id)
            item_path = QStandardItem(r.relative_file_path)

            for item in (item_status, item_pname, item_pval, item_cat, item_uid, item_path):
                item.setEditable(False)

            if is_empty:
                item_status.setForeground(QColor(WARNING))
                item_pval.setForeground(QColor(TEXT_MUTED))
            else:
                item_status.setForeground(QColor(SUCCESS))

            self.model_audit.appendRow([
                item_status, item_pname, item_pval, item_cat, item_uid, item_path
            ])

    def _filter_diff_results(self, text: str):
        text = text.lower()
        for i in range(self.model_diff.rowCount()):
            match = False
            for j in range(self.model_diff.columnCount()):
                item = self.model_diff.item(i, j)
                if item and text in item.text().lower():
                    match = True
                    break
            self.table_diff.setRowHidden(i, not match)

    # ------------------------------------------------------------------
    # EXPORTING METHODS
    # ------------------------------------------------------------------
    def _export_audit_csv(self):
        path, _ = QFileDialog.getSaveFileName(self, "Export Parameter Audit", "", "CSV Files (*.csv);;All Files (*)")
        if not path:
            return

        try:
            with open(path, 'w', newline='', encoding='utf-8') as f:
                writer = csv.writer(f)
                writer.writerow([
                    "Status", "Parameter Name", "Parameter Value", "Category", "Element UniqueId", "File Path"
                ])
                for r in self.filtered_param_records:
                    val = (r.parameter_value or "").strip()
                    status = "EMPTY" if not val else "FILLED"
                    writer.writerow([
                        status, r.parameter_name, val, r.category, r.unique_id, r.relative_file_path
                    ])
            logger.info(f"Exported parameter audit to {path}")
        except Exception as e:
            logger.error(f"Failed to export audit CSV: {e}")

    def _export_diff_csv(self):
        path, _ = QFileDialog.getSaveFileName(self, "Export Change Logs", "", "CSV Files (*.csv);;All Files (*)")
        if not path:
            return

        try:
            with open(path, 'w', newline='', encoding='utf-8') as f:
                writer = csv.writer(f)
                headers = [self.model_diff.horizontalHeaderItem(i).text() for i in range(self.model_diff.columnCount())]
                writer.writerow(headers)

                for i in range(self.model_diff.rowCount()):
                    row_data = []
                    for j in range(self.model_diff.columnCount()):
                        item = self.model_diff.item(i, j)
                        row_data.append(item.text() if item else "")
                    writer.writerow(row_data)
        except Exception as e:
            logger.error(f"Export diff failed: {e}")

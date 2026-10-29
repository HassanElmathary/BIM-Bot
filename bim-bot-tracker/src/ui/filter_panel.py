"""Filter configuration panel."""
from typing import List
from PySide6.QtWidgets import (QGroupBox, QVBoxLayout, QGridLayout, QHBoxLayout, 
                               QCheckBox, QPushButton, QWidget)
from PySide6.QtCore import Signal

from core.models import FilterConfig

class FilterPanel(QGroupBox):
    """Panel for configuring category and parameter filters."""
    
    filters_changed = Signal(FilterConfig)

    def __init__(self, parent=None):
        super().__init__("Filter Settings", parent)
        self.setCheckable(True)
        self.setChecked(True)
        self.toggled.connect(self._emit_filters)
        
        self.categories = [
            "Walls", "Doors", "Windows", "Structural Framing", 
            "Structural Columns", "Floors", "Roofs", "Ceilings", 
            "Mechanical Equipment", "Plumbing Fixtures", 
            "Electrical Equipment", "Pipes", "Ducts"
        ]
        
        self.parameters = [
            "Mark", "Type", "Comments", "Phase Created", 
            "Fire Rating", "Keynote", "Level", "Area", 
            "Volume", "Width", "Height", "Length"
        ]
        
        self.category_checkboxes = []
        self.parameter_checkboxes = []
        
        self._setup_ui()

    def _setup_ui(self):
        main_layout = QVBoxLayout(self)
        
        # Categories Section
        cat_group = QGroupBox("Categories")
        cat_layout = QVBoxLayout(cat_group)
        cat_btns = QHBoxLayout()
        btn_sel_cat = QPushButton("Select All")
        btn_desel_cat = QPushButton("Deselect All")
        btn_sel_cat.clicked.connect(lambda: self._set_all(self.category_checkboxes, True))
        btn_desel_cat.clicked.connect(lambda: self._set_all(self.category_checkboxes, False))
        cat_btns.addWidget(btn_sel_cat)
        cat_btns.addWidget(btn_desel_cat)
        cat_layout.addLayout(cat_btns)
        
        cat_grid = QGridLayout()
        for i, cat in enumerate(self.categories):
            cb = QCheckBox(cat)
            cb.setChecked(True)
            cb.stateChanged.connect(self._emit_filters)
            self.category_checkboxes.append(cb)
            cat_grid.addWidget(cb, i // 2, i % 2)
        cat_layout.addLayout(cat_grid)
        
        # Parameters Section
        param_group = QGroupBox("Parameters")
        param_layout = QVBoxLayout(param_group)
        param_btns = QHBoxLayout()
        btn_sel_param = QPushButton("Select All")
        btn_desel_param = QPushButton("Deselect All")
        btn_sel_param.clicked.connect(lambda: self._set_all(self.parameter_checkboxes, True))
        btn_desel_param.clicked.connect(lambda: self._set_all(self.parameter_checkboxes, False))
        param_btns.addWidget(btn_sel_param)
        param_btns.addWidget(btn_desel_param)
        param_layout.addLayout(param_btns)
        
        param_grid = QGridLayout()
        for i, param in enumerate(self.parameters):
            cb = QCheckBox(param)
            cb.setChecked(True)
            cb.stateChanged.connect(self._emit_filters)
            self.parameter_checkboxes.append(cb)
            param_grid.addWidget(cb, i // 2, i % 2)
        param_layout.addLayout(param_grid)
        
        main_layout.addWidget(cat_group)
        main_layout.addWidget(param_group)
        main_layout.addStretch()
        
    def _set_all(self, checkboxes: List[QCheckBox], state: bool):
        for cb in checkboxes:
            cb.blockSignals(True)
            cb.setChecked(state)
            cb.blockSignals(False)
        self._emit_filters()

    def get_config(self) -> FilterConfig:
        if not self.isChecked():
            return FilterConfig([], [])
            
        selected_cats = [cb.text() for cb in self.category_checkboxes if cb.isChecked()]
        selected_params = [cb.text() for cb in self.parameter_checkboxes if cb.isChecked()]
        return FilterConfig(selected_cats, selected_params)
        
    def _emit_filters(self):
        self.filters_changed.emit(self.get_config())

import logging
from typing import Dict, List, Optional
from .models import ExtractionResult, ElementData, FilterConfig

logger = logging.getLogger(__name__)

# Mapping from Revit Categories to IFC entity types
RVT_TO_IFC_MAPPING = {
    "Walls": "IfcWall",
    "Doors": "IfcDoor",
    "Windows": "IfcWindow",
    "Structural Framing": "IfcBeam",
    "Structural Columns": "IfcColumn",
    "Floors": "IfcSlab",
    "Roofs": "IfcRoof",
    "Ceilings": "IfcCovering",
    "Mechanical Equipment": "IfcEquipmentElement",  # Approximate
    "Plumbing Fixtures": "IfcSanitaryTerminal",
    "Electrical Equipment": "IfcFlowController",
    "Pipes": "IfcFlowSegment",
    "Ducts": "IfcFlowSegment",
}

class IfcExtractor:
    """Wrapper for ifcopenshell to extract IFC data."""
    
    def __init__(self, filter_config: FilterConfig):
        self.filter_config = filter_config

    def extract(self, file_path: str) -> ExtractionResult:
        result = ExtractionResult(status='ERROR', engine_used='ifcopenshell', elements=[])
        
        try:
            import ifcopenshell
            import ifcopenshell.util.element
        except ImportError:
            result.error = "ifcopenshell module is not installed."
            logger.error(result.error)
            return result
            
        try:
            logger.info(f"Extracting IFC elements from {file_path}")
            ifc_file = ifcopenshell.open(file_path)
            
            extracted_elements = []
            
            for category in self.filter_config.categories:
                ifc_type = RVT_TO_IFC_MAPPING.get(category)
                if not ifc_type:
                    continue
                    
                elements = ifc_file.by_type(ifc_type)
                for el in elements:
                    try:
                        unique_id = el.GlobalId
                        name = el.Name if hasattr(el, 'Name') and el.Name else ''
                        
                        el_data = ElementData(
                            unique_id=unique_id,
                            category=category,
                            element_name=name,
                            parameters={}
                        )
                        
                        el_data.parameters['UniqueId'] = unique_id
                        
                        # Get all properties
                        psets = ifcopenshell.util.element.get_psets(el)
                        
                        # Flatten properties for easy lookup
                        flat_props = {}
                        for pset_name, pset_dict in psets.items():
                            for prop_name, prop_val in pset_dict.items():
                                flat_props[prop_name] = prop_val
                                
                        for param_name in self.filter_config.parameters:
                            if param_name == 'UniqueId':
                                continue
                            val = flat_props.get(param_name)
                            if val is None:
                                el_data.parameters[param_name] = ''
                            else:
                                el_data.parameters[param_name] = str(val).strip()
                                
                        extracted_elements.append(el_data)
                    except Exception as e:
                        logger.warning(f"Error extracting an element in category {category}: {e}")
                        result.warnings.append(str(e))
                        
            result.elements = extracted_elements
            result.element_count = len(extracted_elements)
            result.status = 'SUCCESS'
            
        except Exception as e:
            logger.error(f"Failed to extract from {file_path} using ifcopenshell: {e}")
            result.error = str(e)
            
        return result

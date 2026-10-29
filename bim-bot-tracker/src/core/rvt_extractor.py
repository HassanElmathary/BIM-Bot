"""Revit parameter extractor using Revit's built-in RevitExtractor.exe + aps_toolkit.

This module handles the full pipeline:
1. Detect the Revit version from the .rvt OLE file
2. Run RevitExtractor.exe to produce SVF property database
3. Use aps_toolkit.PropDbReaderRevit to read all element parameters
4. Return structured element/parameter data

Fallback: If RevitExtractor.exe fails (missing RCE), we attempt
to find and read a matching exported .ifc file.
"""

import logging
import os
import re
import shutil
import subprocess
import tempfile
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import olefile

from .models import ExtractionResult, ElementData, FilterConfig

logger = logging.getLogger(__name__)

# Maps Revit internal category names to human-readable names
CATEGORY_MAP = {
    'Walls': ['Walls', 'Basic Wall', 'Curtain Wall'],
    'Doors': ['Doors'],
    'Windows': ['Windows'],
    'Structural Columns': ['Structural Columns', 'Columns'],
    'Structural Framing': ['Structural Framing'],
    'Floors': ['Floors', 'Floor'],
    'Ceilings': ['Ceilings', 'Ceiling'],
    'Roofs': ['Roofs', 'Roof'],
    'Mechanical Equipment': ['Mechanical Equipment'],
    'Plumbing Fixtures': ['Plumbing Fixtures'],
    'Electrical Equipment': ['Electrical Equipment'],
    'Pipes': ['Pipes', 'Pipe'],
    'Ducts': ['Ducts', 'Duct'],
}


def detect_revit_version(rvt_path: str) -> Optional[str]:
    """Detect the Revit version from a .rvt file's OLE BasicFileInfo stream."""
    try:
        if not olefile.isOleFile(rvt_path):
            logger.warning(f"Not an OLE file: {rvt_path}")
            return None
            
        with olefile.OleFileIO(rvt_path) as ole:
            if not ole.exists("BasicFileInfo"):
                return None
            bfi = ole.openstream("BasicFileInfo")
            raw = bfi.read()
            text = raw.decode("utf-16le", "ignore")
            
            # Try multiple patterns for version detection
            patterns = [
                r"\x04\x00(\d{4})",           # Standard pattern
                r"Revit (\d{4})",              # Explicit version string
                r"Format:\s*(\d{4})",          # Format tag
                r"Build:\s*\d+\.(\d{4})\.",    # Build number
            ]
            for pattern in patterns:
                match = re.search(pattern, text)
                if match:
                    version = match.group(1)
                    if 2015 <= int(version) <= 2030:
                        return version
            
            logger.warning(f"Could not detect Revit version from {rvt_path}")
            return None
    except Exception as e:
        logger.error(f"Error reading OLE from {rvt_path}: {e}")
        return None


def find_revit_extractor_exe(version: str) -> Optional[str]:
    """Find the RevitExtractor.exe for a given Revit version."""
    # Try the exact version first
    path = rf"C:\Program Files\Autodesk\Revit {version}\RevitExtractor\RevitExtractor.exe"
    if os.path.exists(path):
        return path
    
    # Try nearby versions (Revit can sometimes open older files)
    for offset in [1, -1, 2, -2, 3, -3]:
        alt_ver = str(int(version) + offset)
        alt_path = rf"C:\Program Files\Autodesk\Revit {alt_ver}\RevitExtractor\RevitExtractor.exe"
        if os.path.exists(alt_path):
            logger.info(f"Using RevitExtractor from Revit {alt_ver} for file version {version}")
            return alt_path
    
    return None


def check_rce_available(version: str) -> bool:
    """Check if the Revit Core Engine (RCE) is available for a version."""
    rce_path = rf"C:\Program Files\Common Files\Autodesk Shared\Revit Interoperability {version}\Rx"
    return os.path.exists(rce_path)


def find_available_rce() -> Optional[str]:
    """Find any available RCE version installed."""
    shared_path = r"C:\Program Files\Common Files\Autodesk Shared"
    if not os.path.exists(shared_path):
        return None
    
    for item in os.listdir(shared_path):
        if item.startswith("Revit Interoperability"):
            rx_path = os.path.join(shared_path, item, "Rx")
            if os.path.exists(rx_path):
                match = re.search(r"(\d{4})", item)
                if match:
                    return match.group(1)
    return None


def ensure_portable_revit_extractor(version: str) -> Optional[str]:
    """Prepares a portable RevitExtractor configured with local Revit DLLs if RCE path is missing."""
    src_dir = rf"C:\Program Files\Autodesk\Revit {version}\RevitExtractor"
    if not os.path.exists(src_dir):
        return None
        
    rce_path = rf"C:\Program Files\Common Files\Autodesk Shared\Revit Interoperability {version}\Rx"
    if os.path.exists(rce_path):
        return os.path.join(src_dir, "RevitExtractor.exe")
        
    # Standard RCE path is missing! Prepare portable user copy in %APPDATA%/BIM-BOT
    user_appdata = os.getenv('APPDATA') or str(Path.home() / ".bim-bot")
    target_dir = os.path.join(user_appdata, "BIM-BOT", f"RevitExtractor_{version}")
    
    try:
        if not os.path.exists(target_dir):
            logger.info(f"Setting up portable RevitExtractor for Revit {version} in user directory...")
            shutil.copytree(src_dir, target_dir)
            
        cfg_file = os.path.join(target_dir, "RevitExtractor_Client.exe.config")
        if os.path.exists(cfg_file):
            with open(cfg_file, 'r', encoding='utf-8') as f:
                content = f.read()
                
            revit_install_dir = rf"C:\Program Files\Autodesk\Revit {version}"
            if rce_path in content and os.path.exists(revit_install_dir):
                new_content = content.replace(rce_path, revit_install_dir)
                with open(cfg_file, 'w', encoding='utf-8') as f:
                    f.write(new_content)
                logger.info(f"Auto-configured portable RevitExtractor to use {revit_install_dir}")
                
        portable_exe = os.path.join(target_dir, "RevitExtractor.exe")
        if os.path.exists(portable_exe):
            return portable_exe
    except Exception as e:
        logger.warning(f"Could not prepare portable RevitExtractor: {e}")
        
    return os.path.join(src_dir, "RevitExtractor.exe")


def run_revit_extractor(rvt_path: str, output_dir: str, version: str) -> Optional[str]:
    """Run RevitExtractor.exe to produce SVF output.
    
    Returns the path to the Resource directory on success, None on failure.
    """
    exe_path = ensure_portable_revit_extractor(version) or find_revit_extractor_exe(version)
    if not exe_path:
        logger.warning(f"RevitExtractor.exe not found for Revit {version}")
        return None
    
    try:
        logger.info(f"Running RevitExtractor.exe for {os.path.basename(rvt_path)}")
        result = subprocess.run(
            [exe_path, output_dir, rvt_path],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=600  # 10 min timeout per file
        )
        
        resource_path = os.path.join(output_dir, "output", "Resource")
        if os.path.exists(resource_path):
            logger.info(f"RevitExtractor succeeded for {os.path.basename(rvt_path)}")
            return resource_path
        else:
            logger.warning(f"RevitExtractor did not produce output. Exit code: {result.returncode}")
            if result.stderr:
                logger.debug(f"stderr: {result.stderr[:500]}")
            return None
            
    except subprocess.TimeoutExpired:
        logger.error(f"RevitExtractor timed out for {rvt_path}")
        return None
    except Exception as e:
        logger.error(f"Error running RevitExtractor: {e}")
        return None


def extract_with_propdb(resource_path: str, filter_config: FilterConfig) -> ExtractionResult:
    """Read elements from PropDbReaderRevit after SVF extraction."""
    result = ExtractionResult(status='ERROR', engine_used='revit-extractor', elements=[])
    
    try:
        from aps_toolkit import PropDbReaderRevit
        propdb = PropDbReaderRevit.read_from_resource(resource_path)
        
        # Get all available categories and parameters
        all_categories = propdb.get_all_categories()
        all_params = propdb.get_all_parameters()
        logger.info(f"PropDB: {len(all_categories)} categories, {len(all_params)} parameters available")
        
        # Build the list of categories to query
        query_cats = []
        for user_cat in filter_config.categories:
            # Match user category names to PropDB category names
            variants = CATEGORY_MAP.get(user_cat, [user_cat])
            for cat in all_categories:
                if cat in variants or user_cat.lower() in cat.lower():
                    query_cats.append(cat)
        
        if not query_cats:
            query_cats = all_categories  # If no match, get everything
        
        query_cats = list(set(query_cats))
        logger.info(f"Querying categories: {query_cats}")
        
        # Get data — try get_data_by_categories first
        try:
            if filter_config.parameters:
                df = propdb.get_data_by_categories_and_params(
                    query_cats, filter_config.parameters
                )
            else:
                df = propdb.get_data_by_categories(query_cats)
        except Exception as e:
            logger.warning(f"get_data_by_categories failed: {e}, trying get_all_data")
            df = propdb.get_all_data()
        
        if df is None or df.empty:
            result.status = 'SUCCESS'
            result.element_count = 0
            return result
        
        # Convert DataFrame to ElementData list
        elements = []
        for idx, row in df.iterrows():
            unique_id = str(row.get('externalId', row.get('dbId', idx)))
            category = str(row.get('Category', row.get('category', 'Unknown')))
            name = str(row.get('Name', row.get('name', '')))
            
            params = {}
            for col in df.columns:
                if col not in ('dbId', 'externalId', 'Category', 'category'):
                    val = row[col]
                    if val is None or (isinstance(val, float) and str(val) == 'nan'):
                        params[col] = ''
                    else:
                        params[col] = str(val).strip()
            
            elements.append(ElementData(
                unique_id=unique_id,
                category=category,
                element_name=name,
                parameters=params
            ))
        
        result.elements = elements
        result.element_count = len(elements)
        result.status = 'SUCCESS'
        logger.info(f"Extracted {len(elements)} elements with parameters")
        
    except ImportError:
        result.error = "aps_toolkit not installed"
        logger.error(result.error)
    except Exception as e:
        result.error = str(e)
        logger.error(f"PropDB extraction failed: {e}", exc_info=True)
    
    return result


class RvtExtractor:
    """Extracts element parameters from .rvt files using RevitExtractor.exe + PropDbReaderRevit.
    
    Pipeline:
    1. Detect Revit version from OLE header
    2. Run RevitExtractor.exe to produce SVF property database
    3. Read PropDbReaderRevit from the SVF Resource directory
    4. Return all element parameters for filtered categories
    """
    
    def __init__(self, filter_config: FilterConfig):
        self.filter_config = filter_config
    
    def extract(self, file_path: str) -> ExtractionResult:
        """Extract element parameters from a .rvt file."""
        result = ExtractionResult(status='ERROR', engine_used='revit-extractor', elements=[])
        
        # Step 1: Detect version
        version = detect_revit_version(file_path)
        if not version:
            result.error = "Could not detect Revit version from file"
            return result
        
        logger.info(f"Detected Revit {version} for {os.path.basename(file_path)}")
        
        # Step 2: Run RevitExtractor.exe to produce SVF
        temp_dir = tempfile.mkdtemp(prefix="bimbot_")
        try:
            resource_path = run_revit_extractor(file_path, temp_dir, version)
            
            if not resource_path:
                result.error = (
                    f"RevitExtractor.exe failed for Revit {version}. "
                    f"Please ensure 'Revit Interoperability {version}' is installed. "
                    f"Falling back to IFC extraction if available."
                )
                result.status = 'ERROR'
                return result
            
            # Step 3: Read property database
            result = extract_with_propdb(resource_path, self.filter_config)
            
        finally:
            # Cleanup temp files
            try:
                shutil.rmtree(temp_dir, ignore_errors=True)
            except Exception:
                pass
        
        return result

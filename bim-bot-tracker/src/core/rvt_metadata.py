import os
import logging
import olefile
from typing import Optional
from .models import FileMetadata

logger = logging.getLogger(__name__)

class RvtMetadataReader:
    """Reads metadata from Revit files using OLE2 format."""
    
    @staticmethod
    def read_metadata(file_path: str) -> FileMetadata:
        """Extract metadata from the BasicFileInfo stream of a Revit file."""
        metadata = FileMetadata()
        try:
            stat = os.stat(file_path)
            metadata.file_size = stat.st_size
            metadata.modified_time = stat.st_mtime
        except OSError as e:
            logger.error(f"Failed to get file stats for {file_path}: {e}")
            return metadata

        if not olefile.isOleFile(file_path):
            logger.warning(f"File {file_path} is not a valid OLE2 file.")
            return metadata

        try:
            with olefile.OleFileIO(file_path) as ole:
                if ole.exists('BasicFileInfo'):
                    stream = ole.openstream('BasicFileInfo')
                    data = stream.read()
                    
                    # BasicFileInfo is typically UTF-16LE encoded
                    try:
                        text_data = data.decode('utf-16-le', errors='ignore')
                        # It is typically null-terminated and contains key-value strings
                        # or a structured text format. We'll extract some known patterns.
                        # Simple parsing: find null separated strings and look for keywords.
                        parts = text_data.split('\x00')
                        for part in parts:
                            if 'Format' in part or 'Format:' in part or 'Revit Build' in part:
                                # For demonstration, parse out specific string patterns.
                                # The exact format varies, but usually it's "Format: 2024" or similar.
                                pass
                            
                        # More robust parsing depending on the actual Revit BasicFileInfo structure:
                        # Usually it's key: value pairs delimited by \x00 or \r\n
                        # Let's extract everything looking like a key: value
                        import re
                        # Format is often like "Format\x002024\x00" or "Format  2024"
                        # We will try a generic regex or split lines
                        text_content = data.decode('utf-16-le', errors='replace')
                        
                        match_version = re.search(r'Format(?:[:\s\0]+)?(20\d{2})', text_content, re.IGNORECASE)
                        if match_version:
                            metadata.revit_version = match_version.group(1)
                            
                        match_build = re.search(r'Build(?:[:\s\0]+)?([\w\.-]+)', text_content, re.IGNORECASE)
                        if match_build:
                            metadata.build_number = match_build.group(1)
                            
                        # Optional: Try to find central model path / project name
                        match_proj = re.search(r'Central Model Path(?:[:\s\0]+)?([^\0]+)', text_content, re.IGNORECASE)
                        if match_proj:
                            metadata.project_name = os.path.basename(match_proj.group(1).strip())
                            
                    except Exception as e:
                        logger.error(f"Error parsing BasicFileInfo text in {file_path}: {e}")
        except Exception as e:
            logger.error(f"Failed to read OLE streams from {file_path}: {e}")
            
        return metadata

"""Database manager for BIM-BOT."""

import os
import sqlite3
import logging
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional, List, Dict, Any

from .models import (
    ScanSessionRecord, 
    ModelSnapshotRecord, 
    ElementParameterRecord, 
    ChangeLogRecord
)
from . import queries as q

logger = logging.getLogger(__name__)

class BimDatabase:
    """Main database class for BIM-BOT."""

    def __init__(self, db_path: Optional[str] = None):
        """Initialize database connection.
        
        Args:
            db_path: Path to SQLite DB. Defaults to %AppData%/BIM-BOT/bim_tracker.db
        """
        if db_path is None:
            app_data = os.getenv('APPDATA')
            if app_data:
                db_dir = Path(app_data) / "BIM-BOT"
            else:
                db_dir = Path.home() / ".bim-bot"
            db_dir.mkdir(parents=True, exist_ok=True)
            self.db_path = str(db_dir / "bim_tracker.db")
        else:
            self.db_path = db_path

        self.conn: Optional[sqlite3.Connection] = None
        self._init_db()

    def _init_db(self) -> None:
        """Create schema if it does not exist."""
        try:
            with sqlite3.connect(self.db_path) as conn:
                conn.execute("PRAGMA journal_mode = WAL;")
                conn.execute("PRAGMA foreign_keys = ON;")
                conn.executescript(q.CREATE_TABLES)
                logger.info(f"Database initialized at {self.db_path}")
        except sqlite3.Error as e:
            logger.error(f"Error initializing database schema: {e}")
            raise

    def connect(self) -> None:
        """Establish database connection."""
        if self.conn is None:
            try:
                self.conn = sqlite3.connect(
                    self.db_path, 
                    check_same_thread=False,
                    isolation_level=None  # Manage transactions manually
                )
                self.conn.row_factory = sqlite3.Row
                self.conn.execute("PRAGMA foreign_keys = ON;")
            except sqlite3.Error as e:
                logger.error(f"Failed to connect to database at {self.db_path}: {e}")
                raise

    def close(self) -> None:
        """Close database connection."""
        if self.conn:
            try:
                self.conn.close()
                self.conn = None
            except sqlite3.Error as e:
                logger.error(f"Error closing database: {e}")

    def __enter__(self):
        """Context manager entry."""
        self.connect()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """Context manager exit."""
        self.close()

    def begin_transaction(self) -> None:
        """Begin a database transaction."""
        self._ensure_connected()
        try:
            self.conn.execute("BEGIN TRANSACTION;")
        except sqlite3.Error as e:
            logger.error(f"Error beginning transaction: {e}")
            raise

    def commit(self) -> None:
        """Commit the current transaction."""
        self._ensure_connected()
        try:
            self.conn.execute("COMMIT;")
        except sqlite3.Error as e:
            logger.error(f"Error committing transaction: {e}")
            raise

    def rollback(self) -> None:
        """Rollback the current transaction."""
        self._ensure_connected()
        try:
            self.conn.execute("ROLLBACK;")
        except sqlite3.Error as e:
            logger.error(f"Error rolling back transaction: {e}")
            raise

    def _ensure_connected(self) -> None:
        """Ensure the connection is active."""
        if self.conn is None:
            self.connect()

    def create_session(self, root_folder: str, total_files: int = 0) -> int:
        """Create a new scan session.
        
        Args:
            root_folder: The root folder being scanned.
            total_files: Optional total number of files to scan.
            
        Returns:
            int: The created session ID.
        """
        self._ensure_connected()
        scan_start = datetime.now(timezone.utc).isoformat()
        try:
            cursor = self.conn.execute(
                q.INSERT_SCAN_SESSION,
                (root_folder, scan_start, total_files, 0, 0, 'IN_PROGRESS')
            )
            session_id = cursor.lastrowid
            return session_id
        except sqlite3.Error as e:
            logger.error(f"Error creating session: {e}")
            raise

    def update_session(self, session_id: int, status: str, files_succeeded: int, files_skipped: int, scan_end: Optional[str] = None) -> None:
        """Update an existing scan session."""
        self._ensure_connected()
        try:
            self.conn.execute(
                q.UPDATE_SCAN_SESSION,
                (status, files_succeeded, files_skipped, scan_end, session_id)
            )
        except sqlite3.Error as e:
            logger.error(f"Error updating session {session_id}: {e}")
            raise

    def add_model_snapshot(
        self, 
        session_id: int, 
        relative_file_path: str, 
        file_size: int,
        file_modified: Optional[str],
        revit_version: Optional[str],
        extraction_engine: str, 
        element_count: int
    ) -> int:
        """Add a model snapshot record."""
        self._ensure_connected()
        scan_timestamp = datetime.now(timezone.utc).isoformat()
        try:
            cursor = self.conn.execute(
                q.INSERT_MODEL_SNAPSHOT,
                (
                    session_id, relative_file_path, file_size, file_modified, 
                    revit_version, extraction_engine, scan_timestamp, element_count
                )
            )
            return cursor.lastrowid
        except sqlite3.Error as e:
            logger.error(f"Error adding model snapshot for {relative_file_path}: {e}")
            raise

    def get_parameters_for_file(self, relative_file_path: str) -> List[ElementParameterRecord]:
        """Get all element parameters for a specific file."""
        self._ensure_connected()
        try:
            cursor = self.conn.execute(q.GET_PARAMETERS_FOR_FILE, (relative_file_path,))
            rows = cursor.fetchall()
            return [
                ElementParameterRecord(
                    id=row['id'],
                    relative_file_path=row['relative_file_path'],
                    unique_id=row['unique_id'],
                    category=row['category'],
                    element_name=row['element_name'],
                    parameter_name=row['parameter_name'],
                    parameter_value=row['parameter_value'],
                    last_updated=row['last_updated']
                ) for row in rows
            ]
        except sqlite3.Error as e:
            logger.error(f"Error getting parameters for file {relative_file_path}: {e}")
            raise

    def upsert_parameter(self, record: ElementParameterRecord) -> None:
        """Insert or replace a single element parameter."""
        self._ensure_connected()
        try:
            self.conn.execute(
                q.UPSERT_ELEMENT_PARAMETER,
                (
                    record.relative_file_path, record.unique_id, record.category,
                    record.element_name, record.parameter_name, record.parameter_value,
                    record.last_updated
                )
            )
        except sqlite3.Error as e:
            logger.error(f"Error upserting parameter {record.parameter_name} for {record.unique_id}: {e}")
            raise

    def batch_upsert_parameters(self, records: List[ElementParameterRecord]) -> None:
        """Efficiently batch insert or update parameters."""
        if not records:
            return
        
        self._ensure_connected()
        data = [
            (
                r.relative_file_path, r.unique_id, r.category,
                r.element_name, r.parameter_name, r.parameter_value,
                r.last_updated
            ) for r in records
        ]
        
        try:
            self.begin_transaction()
            self.conn.executemany(q.UPSERT_ELEMENT_PARAMETER, data)
            self.commit()
        except sqlite3.Error as e:
            self.rollback()
            logger.error(f"Error in batch upsert parameters: {e}")
            raise

    def delete_parameters_for_element(self, relative_file_path: str, unique_id: str) -> None:
        """Delete all parameters for a specific element."""
        self._ensure_connected()
        try:
            self.conn.execute(q.DELETE_PARAMETERS_FOR_ELEMENT, (relative_file_path, unique_id))
        except sqlite3.Error as e:
            logger.error(f"Error deleting parameters for element {unique_id} in {relative_file_path}: {e}")
            raise

    def add_change_log(self, session_id: int, record: ChangeLogRecord) -> None:
        """Add a single change log record."""
        self._ensure_connected()
        try:
            self.conn.execute(
                q.INSERT_CHANGE_LOG,
                (
                    session_id, record.relative_file_path, record.unique_id,
                    record.category, record.change_type, record.parameter_name,
                    record.old_value, record.new_value, record.detected_at
                )
            )
        except sqlite3.Error as e:
            logger.error(f"Error adding change log for {record.unique_id}: {e}")
            raise

    def batch_add_change_logs(self, session_id: int, records: List[ChangeLogRecord]) -> None:
        """Efficiently batch insert change logs."""
        if not records:
            return
            
        self._ensure_connected()
        data = [
            (
                session_id, r.relative_file_path, r.unique_id,
                r.category, r.change_type, r.parameter_name,
                r.old_value, r.new_value, r.detected_at
            ) for r in records
        ]
        
        try:
            self.begin_transaction()
            self.conn.executemany(q.INSERT_CHANGE_LOG, data)
            self.commit()
        except sqlite3.Error as e:
            self.rollback()
            logger.error(f"Error in batch add change logs: {e}")
            raise

    def get_last_session(self) -> Optional[ScanSessionRecord]:
        """Get the most recent scan session."""
        self._ensure_connected()
        try:
            cursor = self.conn.execute(q.GET_LAST_SESSION)
            row = cursor.fetchone()
            if row:
                return ScanSessionRecord(
                    id=row['id'],
                    root_folder=row['root_folder'],
                    scan_start=row['scan_start'],
                    scan_end=row['scan_end'],
                    total_files=row['total_files'],
                    files_succeeded=row['files_succeeded'],
                    files_skipped=row['files_skipped'],
                    status=row['status']
                )
            return None
        except sqlite3.Error as e:
            logger.error(f"Error getting last session: {e}")
            return None

    def get_all_parameters(self) -> List[ElementParameterRecord]:
        """Get all element parameters from database across all models."""
        self._ensure_connected()
        try:
            cursor = self.conn.execute(q.GET_ALL_ELEMENT_PARAMETERS)
            rows = cursor.fetchall()
            return [
                ElementParameterRecord(
                    id=row['id'],
                    relative_file_path=row['relative_file_path'],
                    unique_id=row['unique_id'],
                    category=row['category'],
                    element_name=row['element_name'],
                    parameter_name=row['parameter_name'],
                    parameter_value=row['parameter_value'],
                    last_updated=row['last_updated']
                ) for row in rows
            ]
        except sqlite3.Error as e:
            logger.error(f"Error fetching all element parameters: {e}")
            return []

    def get_all_change_logs(self) -> List[ChangeLogRecord]:
        """Get all change log records from database."""
        self._ensure_connected()
        try:
            cursor = self.conn.execute(q.GET_ALL_CHANGE_LOGS)
            rows = cursor.fetchall()
            return [
                ChangeLogRecord(
                    id=row['id'],
                    session_id=row['session_id'],
                    relative_file_path=row['relative_file_path'],
                    unique_id=row['unique_id'],
                    category=row['category'],
                    change_type=row['change_type'],
                    parameter_name=row['parameter_name'],
                    old_value=row['old_value'],
                    new_value=row['new_value'],
                    detected_at=row['detected_at']
                ) for row in rows
            ]
        except sqlite3.Error as e:
            logger.error(f"Error fetching all change logs: {e}")
            return []


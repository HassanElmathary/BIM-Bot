"""SQL query constants for BIM-BOT database."""

CREATE_TABLES = """
CREATE TABLE IF NOT EXISTS scan_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    root_folder TEXT NOT NULL,
    scan_start TEXT NOT NULL,
    scan_end TEXT,
    total_files INTEGER DEFAULT 0,
    files_succeeded INTEGER DEFAULT 0,
    files_skipped INTEGER DEFAULT 0,
    status TEXT DEFAULT 'IN_PROGRESS'
);

CREATE TABLE IF NOT EXISTS model_snapshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER,
    relative_file_path TEXT NOT NULL,
    file_size INTEGER DEFAULT 0,
    file_modified TEXT,
    revit_version TEXT,
    extraction_engine TEXT,
    scan_timestamp TEXT NOT NULL,
    element_count INTEGER DEFAULT 0,
    FOREIGN KEY(session_id) REFERENCES scan_sessions(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS element_parameters (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    relative_file_path TEXT NOT NULL,
    unique_id TEXT NOT NULL,
    category TEXT,
    element_name TEXT,
    parameter_name TEXT NOT NULL,
    parameter_value TEXT,
    last_updated TEXT NOT NULL,
    UNIQUE(relative_file_path, unique_id, parameter_name)
);

CREATE TABLE IF NOT EXISTS change_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER,
    relative_file_path TEXT NOT NULL,
    unique_id TEXT NOT NULL,
    category TEXT,
    change_type TEXT NOT NULL,
    parameter_name TEXT,
    old_value TEXT,
    new_value TEXT,
    detected_at TEXT NOT NULL,
    FOREIGN KEY(session_id) REFERENCES scan_sessions(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_model_snapshots_session ON model_snapshots(session_id);
CREATE INDEX IF NOT EXISTS idx_model_snapshots_file ON model_snapshots(relative_file_path);

CREATE INDEX IF NOT EXISTS idx_element_parameters_file ON element_parameters(relative_file_path);
CREATE INDEX IF NOT EXISTS idx_element_parameters_file_uid ON element_parameters(relative_file_path, unique_id);

CREATE INDEX IF NOT EXISTS idx_change_logs_session ON change_logs(session_id);
CREATE INDEX IF NOT EXISTS idx_change_logs_file ON change_logs(relative_file_path);
"""

# Scan Sessions
INSERT_SCAN_SESSION = """
INSERT INTO scan_sessions (root_folder, scan_start, total_files, files_succeeded, files_skipped, status)
VALUES (?, ?, ?, ?, ?, ?)
"""

UPDATE_SCAN_SESSION = """
UPDATE scan_sessions 
SET status = ?, files_succeeded = ?, files_skipped = ?, scan_end = ?
WHERE id = ?
"""

GET_LAST_SESSION = """
SELECT id, root_folder, scan_start, scan_end, total_files, files_succeeded, files_skipped, status
FROM scan_sessions
ORDER BY scan_start DESC
LIMIT 1
"""

# Model Snapshots
INSERT_MODEL_SNAPSHOT = """
INSERT INTO model_snapshots (session_id, relative_file_path, file_size, file_modified, revit_version, extraction_engine, scan_timestamp, element_count)
VALUES (?, ?, ?, ?, ?, ?, ?, ?)
"""

# Element Parameters
GET_PARAMETERS_FOR_FILE = """
SELECT id, relative_file_path, unique_id, category, element_name, parameter_name, parameter_value, last_updated
FROM element_parameters
WHERE relative_file_path = ?
"""

UPSERT_ELEMENT_PARAMETER = """
INSERT INTO element_parameters (relative_file_path, unique_id, category, element_name, parameter_name, parameter_value, last_updated)
VALUES (?, ?, ?, ?, ?, ?, ?)
ON CONFLICT(relative_file_path, unique_id, parameter_name) 
DO UPDATE SET 
    category = excluded.category,
    element_name = excluded.element_name,
    parameter_value = excluded.parameter_value,
    last_updated = excluded.last_updated
"""

DELETE_PARAMETERS_FOR_ELEMENT = """
DELETE FROM element_parameters
WHERE relative_file_path = ? AND unique_id = ?
"""

# Change Logs
INSERT_CHANGE_LOG = """
INSERT INTO change_logs (session_id, relative_file_path, unique_id, category, change_type, parameter_name, old_value, new_value, detected_at)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
"""

GET_ALL_ELEMENT_PARAMETERS = """
SELECT id, relative_file_path, unique_id, category, element_name, parameter_name, parameter_value, last_updated
FROM element_parameters
ORDER BY relative_file_path, category, unique_id, parameter_name
"""

GET_ALL_CHANGE_LOGS = """
SELECT id, session_id, relative_file_path, unique_id, category, change_type, parameter_name, old_value, new_value, detected_at
FROM change_logs
ORDER BY detected_at DESC
"""


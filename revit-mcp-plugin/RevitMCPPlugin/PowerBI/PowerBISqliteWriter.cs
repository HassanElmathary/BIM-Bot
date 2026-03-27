using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace RevitMCPPlugin.PowerBI
{
    /// <summary>
    /// Writes extracted Revit element data and 3D geometry to a local SQLite database
    /// for consumption by Power BI. Uses batched transactions for performance.
    /// </summary>
    public class PowerBISqliteWriter
    {
        private const string DefaultDbName = "RevitMCP_PowerBI.db";

        /// <summary>
        /// Write all export data to a SQLite database.
        /// </summary>
        /// <param name="elements">Element metadata</param>
        /// <param name="meshData">ElementId → MeshData geometry</param>
        /// <param name="categoryColors">Category → RGB color</param>
        /// <param name="metadata">Key/value metadata (project name, export date, etc.)</param>
        /// <param name="dbPath">Path to the .db file (created if not exists)</param>
        /// <param name="mode">"new" = drop+create tables, "update" = upsert</param>
        /// <returns>Summary of the export</returns>
        public ExportResult Write(
            List<ElementExportData> elements,
            Dictionary<int, MeshData> meshData,
            Dictionary<string, (int R, int G, int B)> categoryColors,
            Dictionary<string, string> metadata,
            string dbPath,
            string mode = "new")
        {
            // Ensure directory exists
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Initialize SQLite provider
            SQLitePCL.Batteries_V2.Init();

            var connStr = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            using (var conn = new SqliteConnection(connStr))
            {
                conn.Open();

                if (mode == "new")
                {
                    DropAndCreateTables(conn);
                }
                else
                {
                    EnsureTablesExist(conn);
                }

                int elementsWritten = 0;
                int paramsWritten = 0;
                int geomWritten = 0;

                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Write Elements
                        elementsWritten = WriteElements(conn, tx, elements, mode);

                        // 2. Write Parameters
                        paramsWritten = WriteParameters(conn, tx, elements, mode);

                        // 3. Write Geometry
                        geomWritten = WriteGeometry(conn, tx, meshData, mode);

                        // 4. Write CategoryColors
                        WriteCategoryColors(conn, tx, categoryColors, mode);

                        // 5. Write Metadata
                        WriteMetadata(conn, tx, metadata, mode);

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }

                var fileInfo = new FileInfo(dbPath);

                return new ExportResult
                {
                    DbPath = dbPath,
                    ElementCount = elementsWritten,
                    ParameterCount = paramsWritten,
                    GeometryCount = geomWritten,
                    FileSizeBytes = fileInfo.Length,
                    Mode = mode
                };
            }
        }

        /// <summary>
        /// Resolve the database path. If no explicit path provided, use data/ folder.
        /// </summary>
        public static string ResolveDbPath(string dbPath, string dataFolder)
        {
            if (!string.IsNullOrWhiteSpace(dbPath))
                return dbPath;

            if (!Directory.Exists(dataFolder))
                Directory.CreateDirectory(dataFolder);

            return Path.Combine(dataFolder, DefaultDbName);
        }

        // ═══════════════════════════════════════════════════
        //  Schema
        // ═══════════════════════════════════════════════════

        private void DropAndCreateTables(SqliteConnection conn)
        {
            var sql = @"
                DROP TABLE IF EXISTS Parameters;
                DROP TABLE IF EXISTS Geometry;
                DROP TABLE IF EXISTS CategoryColors;
                DROP TABLE IF EXISTS ExportMetadata;
                DROP TABLE IF EXISTS Elements;

                CREATE TABLE Elements (
                    ElementId   INTEGER PRIMARY KEY,
                    Category    TEXT NOT NULL,
                    FamilyName  TEXT,
                    TypeName    TEXT,
                    LevelName   TEXT,
                    Mark        TEXT
                );

                CREATE TABLE Parameters (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    ElementId   INTEGER NOT NULL,
                    ParamName   TEXT NOT NULL,
                    ParamValue  TEXT,
                    FOREIGN KEY (ElementId) REFERENCES Elements(ElementId)
                );

                CREATE TABLE Geometry (
                    ElementId    INTEGER PRIMARY KEY,
                    MeshJSON     TEXT NOT NULL,
                    VertexCount  INTEGER,
                    FaceCount    INTEGER,
                    FOREIGN KEY (ElementId) REFERENCES Elements(ElementId)
                );

                CREATE TABLE CategoryColors (
                    Category TEXT PRIMARY KEY,
                    R INTEGER NOT NULL,
                    G INTEGER NOT NULL,
                    B INTEGER NOT NULL
                );

                CREATE TABLE ExportMetadata (
                    Key   TEXT PRIMARY KEY,
                    Value TEXT
                );

                CREATE INDEX IF NOT EXISTS idx_params_elementid ON Parameters(ElementId);
                CREATE INDEX IF NOT EXISTS idx_elements_category ON Elements(Category);
                CREATE INDEX IF NOT EXISTS idx_elements_level ON Elements(LevelName);
            ";

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private void EnsureTablesExist(SqliteConnection conn)
        {
            // Only create if they don't exist — for "update" mode
            var sql = @"
                CREATE TABLE IF NOT EXISTS Elements (
                    ElementId   INTEGER PRIMARY KEY,
                    Category    TEXT NOT NULL,
                    FamilyName  TEXT,
                    TypeName    TEXT,
                    LevelName   TEXT,
                    Mark        TEXT
                );

                CREATE TABLE IF NOT EXISTS Parameters (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    ElementId   INTEGER NOT NULL,
                    ParamName   TEXT NOT NULL,
                    ParamValue  TEXT,
                    FOREIGN KEY (ElementId) REFERENCES Elements(ElementId)
                );

                CREATE TABLE IF NOT EXISTS Geometry (
                    ElementId    INTEGER PRIMARY KEY,
                    MeshJSON     TEXT NOT NULL,
                    VertexCount  INTEGER,
                    FaceCount    INTEGER,
                    FOREIGN KEY (ElementId) REFERENCES Elements(ElementId)
                );

                CREATE TABLE IF NOT EXISTS CategoryColors (
                    Category TEXT PRIMARY KEY,
                    R INTEGER NOT NULL,
                    G INTEGER NOT NULL,
                    B INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ExportMetadata (
                    Key   TEXT PRIMARY KEY,
                    Value TEXT
                );

                CREATE INDEX IF NOT EXISTS idx_params_elementid ON Parameters(ElementId);
                CREATE INDEX IF NOT EXISTS idx_elements_category ON Elements(Category);
                CREATE INDEX IF NOT EXISTS idx_elements_level ON Elements(LevelName);
            ";

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        // ═══════════════════════════════════════════════════
        //  Batched Writers
        // ═══════════════════════════════════════════════════

        private int WriteElements(SqliteConnection conn, SqliteTransaction tx,
            List<ElementExportData> elements, string mode)
        {
            var upsert = mode == "update"
                ? " ON CONFLICT(ElementId) DO UPDATE SET Category=excluded.Category, FamilyName=excluded.FamilyName, TypeName=excluded.TypeName, LevelName=excluded.LevelName, Mark=excluded.Mark"
                : "";

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = $@"
                    INSERT INTO Elements (ElementId, Category, FamilyName, TypeName, LevelName, Mark)
                    VALUES ($id, $cat, $fam, $type, $level, $mark){upsert}";

                var pId = cmd.Parameters.Add("$id", SqliteType.Integer);
                var pCat = cmd.Parameters.Add("$cat", SqliteType.Text);
                var pFam = cmd.Parameters.Add("$fam", SqliteType.Text);
                var pType = cmd.Parameters.Add("$type", SqliteType.Text);
                var pLevel = cmd.Parameters.Add("$level", SqliteType.Text);
                var pMark = cmd.Parameters.Add("$mark", SqliteType.Text);

                int count = 0;
                foreach (var elem in elements)
                {
                    pId.Value = elem.ElementId;
                    pCat.Value = elem.Category ?? (object)DBNull.Value;
                    pFam.Value = elem.FamilyName ?? (object)DBNull.Value;
                    pType.Value = elem.TypeName ?? (object)DBNull.Value;
                    pLevel.Value = elem.LevelName ?? (object)DBNull.Value;
                    pMark.Value = elem.Mark ?? (object)DBNull.Value;
                    cmd.ExecuteNonQuery();
                    count++;
                }
                return count;
            }
        }

        private int WriteParameters(SqliteConnection conn, SqliteTransaction tx,
            List<ElementExportData> elements, string mode)
        {
            if (mode == "update")
            {
                // Delete old parameters for these elements, then re-insert
                using (var delCmd = conn.CreateCommand())
                {
                    delCmd.Transaction = tx;
                    delCmd.CommandText = "DELETE FROM Parameters WHERE ElementId = $id";
                    var pDelId = delCmd.Parameters.Add("$id", SqliteType.Integer);

                    foreach (var elem in elements)
                    {
                        pDelId.Value = elem.ElementId;
                        delCmd.ExecuteNonQuery();
                    }
                }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO Parameters (ElementId, ParamName, ParamValue)
                    VALUES ($id, $name, $value)";

                var pId = cmd.Parameters.Add("$id", SqliteType.Integer);
                var pName = cmd.Parameters.Add("$name", SqliteType.Text);
                var pValue = cmd.Parameters.Add("$value", SqliteType.Text);

                int count = 0;
                foreach (var elem in elements)
                {
                    foreach (var kvp in elem.Parameters)
                    {
                        pId.Value = elem.ElementId;
                        pName.Value = kvp.Key;
                        pValue.Value = kvp.Value ?? (object)DBNull.Value;
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
                return count;
            }
        }

        private int WriteGeometry(SqliteConnection conn, SqliteTransaction tx,
            Dictionary<int, MeshData> meshData, string mode)
        {
            var upsert = mode == "update"
                ? " ON CONFLICT(ElementId) DO UPDATE SET MeshJSON=excluded.MeshJSON, VertexCount=excluded.VertexCount, FaceCount=excluded.FaceCount"
                : "";

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = $@"
                    INSERT INTO Geometry (ElementId, MeshJSON, VertexCount, FaceCount)
                    VALUES ($id, $json, $vc, $fc){upsert}";

                var pId = cmd.Parameters.Add("$id", SqliteType.Integer);
                var pJson = cmd.Parameters.Add("$json", SqliteType.Text);
                var pVc = cmd.Parameters.Add("$vc", SqliteType.Integer);
                var pFc = cmd.Parameters.Add("$fc", SqliteType.Integer);

                int count = 0;
                foreach (var kvp in meshData)
                {
                    pId.Value = kvp.Key;
                    pJson.Value = kvp.Value.ToJson();
                    pVc.Value = kvp.Value.VertexCount;
                    pFc.Value = kvp.Value.FaceCount;
                    cmd.ExecuteNonQuery();
                    count++;
                }
                return count;
            }
        }

        private void WriteCategoryColors(SqliteConnection conn, SqliteTransaction tx,
            Dictionary<string, (int R, int G, int B)> colors, string mode)
        {
            var upsert = mode == "update"
                ? " ON CONFLICT(Category) DO UPDATE SET R=excluded.R, G=excluded.G, B=excluded.B"
                : "";

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = $@"
                    INSERT INTO CategoryColors (Category, R, G, B)
                    VALUES ($cat, $r, $g, $b){upsert}";

                var pCat = cmd.Parameters.Add("$cat", SqliteType.Text);
                var pR = cmd.Parameters.Add("$r", SqliteType.Integer);
                var pG = cmd.Parameters.Add("$g", SqliteType.Integer);
                var pB = cmd.Parameters.Add("$b", SqliteType.Integer);

                foreach (var kvp in colors)
                {
                    pCat.Value = kvp.Key;
                    pR.Value = kvp.Value.R;
                    pG.Value = kvp.Value.G;
                    pB.Value = kvp.Value.B;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void WriteMetadata(SqliteConnection conn, SqliteTransaction tx,
            Dictionary<string, string> metadata, string mode)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO ExportMetadata (Key, Value) VALUES ($key, $val)
                    ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value";

                var pKey = cmd.Parameters.Add("$key", SqliteType.Text);
                var pVal = cmd.Parameters.Add("$val", SqliteType.Text);

                foreach (var kvp in metadata)
                {
                    pKey.Value = kvp.Key;
                    pVal.Value = kvp.Value ?? (object)DBNull.Value;
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }

    /// <summary>Export result summary.</summary>
    public class ExportResult
    {
        public string DbPath { get; set; }
        public int ElementCount { get; set; }
        public int ParameterCount { get; set; }
        public int GeometryCount { get; set; }
        public long FileSizeBytes { get; set; }
        public string Mode { get; set; }

        public string FileSizeFormatted
        {
            get
            {
                if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
                if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024.0:F1} KB";
                return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
            }
        }
    }
}

using Serilog;
using System;
using System.Data.SQLite;
using System.IO;

namespace WinFIM.NET_Service
{
    internal class SQLiteHelper : IDisposable
    {
        private string ConnectionString { get; }
        private string DbFilePath { get; }
        private const int CurrentDatabaseVersion = 3;
        private const string CurrentDatabaseVersionNotes =
            "capitalised table names," +
            "renamed field fileowner to owner," +
            "renamed field filetype to pathtype," +
            "added field pathexists to tables BASELINE_PATH and CURRENT_PATH," +
            "added table VERSION_CONTROL," +
            "removed table monlist," +
            "renamed table baseline_table to BASELINE_PATH," +
            "renamed table current_table  to CURRENT_PATH";
        internal SQLiteConnection Connection { get; }

        private bool _disposed;

        internal SQLiteHelper()
        {
            DbFilePath = LogHelper.WorkDir + "\\fimdb.db";
            ConnectionString = @"URI=file:" + DbFilePath + ";PRAGMA journal_mode=WAL;";
            Connection = new SQLiteConnection(ConnectionString);
            EnsureDatabaseExists();
        }

        private void EnsureDatabaseExists()
        // Create the database if it doesn't exist or is the wrong version
        {
            if (File.Exists(DbFilePath))
            {
                Log.Debug($"SQLite database file {DbFilePath} exists");
                Connection.Open();
                int checkedDatabaseVersion = CheckDatabaseVersion();
                if (checkedDatabaseVersion != CurrentDatabaseVersion)
                {
                    string dbFileName = Path.GetFileNameWithoutExtension(DbFilePath);
                    string dbFileExt = Path.GetExtension(DbFilePath);
                    string dbDirName = Path.GetDirectoryName(DbFilePath);
                    string currentFileFriendlyDateTime = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    string backupDbFileName = $"{dbFileName}-old-version-v{checkedDatabaseVersion}-{currentFileFriendlyDateTime}{dbFileExt}";
                    string backupDbPath = $"{dbDirName}\\{backupDbFileName}";
                    Log.Information($"SQLite database {DbFilePath} is version {checkedDatabaseVersion}. Required version {CurrentDatabaseVersion}. Renaming to {backupDbPath}");
                    Connection.Close();
                    if (DbFilePath != null) File.Move(DbFilePath, backupDbPath);
                    Connection.Open();
                }
            }
            if (!File.Exists(DbFilePath))
            {
                Log.Information($"Creating SQLite database file {DbFilePath}");
                SQLiteConnection.CreateFile(DbFilePath);
                Connection.Open();
            }
            EnsureTablesExist();
        }

        private int CheckDatabaseVersion()
        {
            Log.Debug("Checking database version");
            int checkedDatabaseVersion = 0;
            try
            {
                string sql = $"SELECT version FROM VERSION_CONTROL order by version desc limit 1";
                object output = ExecuteScalar(sql, false)??0;
                checkedDatabaseVersion = Convert.ToInt32(output); // try convert to integer, or output 0
                Log.Debug($"Database version for {DbFilePath}: {checkedDatabaseVersion}");
            }
            catch
            {
                Log.Debug($"Database version for {DbFilePath} not found. Interpreting as version {checkedDatabaseVersion}");
            }
            return checkedDatabaseVersion;
        }

        private void EnsureTablesExist()
        // Ensure that all required tables exist
        {
            Log.Debug("Creating SQlite table BASELINE_PATH if it doesn't exist...");
            string sql = @"
                CREATE TABLE IF NOT EXISTS BASELINE_PATH (
                    pathname    TEXT PRIMARY KEY,
                    pathexists  BOOLEAN  CHECK (pathexists IN (0, 1)) NOT NULL,
                    filesize    INT,
                    owner       TEXT NOT NULL,
                    checktime   TEXT NOT NULL,
                    filehash    TEXT,
                    pathtype    TEXT NOT NULL
                );";
            ExecuteNonQuery(sql);

            Log.Debug("Creating SQlite table CURRENT_PATH if it doesn't exist...");
            sql = @"
                CREATE TABLE IF NOT EXISTS CURRENT_PATH (
                    pathname    TEXT PRIMARY KEY,
                    pathexists  BOOLEAN  CHECK (pathexists IN (0, 1)) NOT NULL,
                    filesize    INT,
                    owner       TEXT NOT NULL,
                    checktime   TEXT NOT NULL,
                    filehash    TEXT,
                    pathtype    TEXT NOT NULL
                );";
            ExecuteNonQuery(sql);

            Log.Debug("Creating SQlite table CONF_FILE_CHECKSUM if it doesn't exist...");
            sql = @"
                CREATE TABLE IF NOT EXISTS CONF_FILE_CHECKSUM (
                    pathname    TEXT PRIMARY KEY,
                    filehash    TEXT
                );";
            ExecuteNonQuery(sql);

            Log.Debug("Creating SQlite table VERSION_CONTROL if it doesn't exist...");
            sql = @"
                CREATE TABLE IF NOT EXISTS VERSION_CONTROL (
                    version     INT PRIMARY KEY,
                    notes       TEXT NOT NULL
                );";
            ExecuteNonQuery(sql);

            Log.Debug("Setting database version...");
            sql = $@"
                INSERT OR REPLACE INTO VERSION_CONTROL (version, notes) 
                VALUES ({CurrentDatabaseVersion}, '{CurrentDatabaseVersionNotes}');
            ";
            ExecuteNonQuery(sql);
        }

        internal void ExecuteNonQuery(string sql)
        {
            try
            {
                using (SQLiteCommand command = new SQLiteCommand(Connection))
                {
                    Log.Verbose($"Running ExecuteNonQuery {sql}");
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                string errorMessage = $"Error running ExecuteNonQuery {sql}";
                Log.Error(e, errorMessage);
                throw;
            }

        }

        // A query that returns the first value in the first row as an object
        internal object ExecuteScalar(string sql, bool isLogError = true)
        {
            object output;
            try
            {
                using (SQLiteCommand command = new SQLiteCommand(Connection))
                {
                    Log.Verbose($"Running ExecuteScalarAsync {sql}");
                    command.CommandText = sql;
                    output = command.ExecuteScalarAsync().Result;
                }
            }
            catch (Exception e)
            {
                if (isLogError)
                {
                    string errorMessage = $"Error running query {sql}";
                    Log.Error(e, errorMessage);
                    throw;
                }
                return null;
            }

            return output;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this._disposed)
            {
                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose managed resources.
                    Connection.Close();
                    Connection.Dispose();
                }

                // Note disposing has been done.
                _disposed = true;
            }
        }
    }
}
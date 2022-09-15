using Serilog;
using System;
using System.Data.SQLite;
using System.IO;

namespace WinFIM.NET_Service
{
    internal static class SQLiteHelper
    {
        internal static void EnsureDatabaseExists(string dbFile)
        // Create the database if it doesn't exist
        {
            if (File.Exists(dbFile))
            {
                Log.Debug($"SQLite database file {dbFile} already exists");
            }
            else
            {
                Log.Warning($"SQLite database file {dbFile} Does not exist. Creating");
                SQLiteConnection.CreateFile(dbFile);
            }
        }

        internal static void EnsureTablesExist(string connectionString)
        // Ensure that all required tables exist
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        Log.Debug("Creating SQlite table baseline_table if it doesn't exist...");
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS baseline_table (
                                pathname    TEXT PRIMARY KEY,
                                filesize    INT,
                                fileowner   TEXT NOT NULL,
                                checktime   TEXT NOT NULL,
                                filehash    TEXT,
                                filetype    TEXT NOT NULL
                            );
                        ";
                        command.ExecuteNonQuery();
                        
                        Log.Debug("Creating SQlite table conf_file_checksum if it doesn't exist...");
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS conf_file_checksum (
                                pathname    TEXT PRIMARY KEY,
                                filehash    TEXT
                            );
                        ";
                        command.ExecuteNonQuery();

                        Log.Debug("Creating SQlite table current_table if it doesn't exist...");
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS current_table (
                                pathname    TEXT PRIMARY KEY,
                                filesize    INT,
                                fileowner   TEXT NOT NULL,
                                checktime   TEXT NOT NULL,
                                filehash    TEXT,
                                filetype    TEXT NOT NULL
                            );
                        ";
                        command.ExecuteNonQuery();

                        Log.Debug("Creating SQlite table monlist if it doesn't exist...");
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS monlist (
                                pathname TEXT PRIMARY KEY,
                                pathexists BOOLEAN  CHECK (pathexists IN (0, 1)) NOT NULL,
                                checktime TEXT NOT NULL
                            );
                        ";
                        command.ExecuteNonQuery();

                        Log.Debug("Creating SQlite table version_control if it doesn't exist...");
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS version_control (
                                version     INT PRIMARY KEY,
                                notes       TEXT NOT NULL
                            );
                        ";
                        command.ExecuteNonQuery();

                        Log.Debug("Setting database version...");
                        command.CommandText = @"
                            INSERT OR REPLACE INTO version_control (version, notes) VALUES (1, 'Initial version');
                            INSERT OR REPLACE INTO version_control (version, notes) VALUES (2, 'changed field filename to pathname and set as primary key;\n added field isbasepath to tables baseline_table, current_table; added tables version_control, monlist_baseline_table, monlist_current_table');
                        ";
                        command.ExecuteNonQuery();
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        internal static void NonQuery(string connectionString, string sql)
        // To test SQL table population
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        Log.Verbose($"Running NonQuery {sql}");
                        command.CommandText = sql;
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception e)
                {
                    string errorMessage = $"Error running NonQuery {sql}";
                    Log.Error(e, errorMessage);
                    throw;
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        internal static string Query(string connectionString, string sql)
        {
            string output = string.Empty;
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        Log.Debug($"Querying {sql}... ");
                        command.CommandText = sql;
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                output = reader.GetValue(0).ToString();
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    string errorMessage = $"Error running query {sql}";
                    Log.Error(e, errorMessage);
                    throw;
                }
                finally
                {
                    connection.Close();
                }
            }
            return output;
        }
    }
}

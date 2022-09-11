using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;
using System.Data;

namespace WinFIM.NET_Service
{
    internal class SQLiteHelper
    {
        internal static void EnsureDatabaseExists(string dbFile)
        // Create the database if it doesn't exist
        {
            if (File.Exists(dbFile))
            {
                Console.WriteLine($"SQLite database file {dbFile} already exists");
            }
            else
            {
                Console.WriteLine($"SQLite database file {dbFile} Does not exist. Creating");
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
                        Console.WriteLine("Creating SQlite table baseline_table if it doesn't exist...");
                        command.CommandText = @"
                            create table if not exists baseline_table (
                                filename  TEXT NOT NULL,
                                filesize  INT,
                                fileowner TEXT NOT NULL,
                                checktime TEXT NOT NULL,
                                filehash  TEXT,
                                filetype  TEXT NOT NULL
                            );
                        ";
                        command.ExecuteNonQuery();

                        Console.WriteLine("Creating SQlite table conf_file_checksum if it doesn't exist...");
                        command.CommandText = @"
                            create table if not exists conf_file_checksum (
                                filename TEXT NOT NULL,
                                filehash TEXT
                            );
                        ";
                        command.ExecuteNonQuery();

                        Console.WriteLine("Creating SQlite table current_table if it doesn't exist...");
                        command.CommandText = @"
                            create table if not exists current_table (
                                filename  TEXT NOT NULL,
                                filesize  INT,
                                fileowner TEXT NOT NULL,
                                checktime TEXT NOT NULL,
                                filehash  TEXT,
                                filetype  TEXT NOT NULL
                            );
                        ";
                        command.ExecuteNonQuery();

                        Console.WriteLine("Creating SQlite table monlist if it doesn't exist...");
                        command.CommandText = @"
                            create table if not exists monlist (
                                pathname TEXT PRIMARY KEY,
                                pathexists BOOLEAN  CHECK (pathexists IN (0, 1)),
                                checktime TEXT NOT NULL
                            );
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


        internal static void InsertOrReplaceInMonListTable(string connectionString, string pathName, bool pathExists)
        // To test SQL table population
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        Log.Info($"Inserting path {pathName}, exists:{pathExists} into SQLite table monlist...");
                        command.Parameters.Add("@pathName", System.Data.DbType.String).Value = pathName;
                        command.Parameters.Add("@pathExists", System.Data.DbType.Boolean).Value = pathExists;
                        command.Parameters.Add("@checkTime", System.Data.DbType.String).Value = DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt");
                        command.CommandText = @"insert or replace into monlist (pathname, pathexists, checktime) values (@pathName, @pathExists, @checkTime);";
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"couldn't write stuff - {ex.Message}");
                }
                finally
                {
                    connection.Close();
                }
            }
        }
        internal static string QueryMonListTable(string connectionString, string pathName)
        // Run an SQL query on a table
        {
            string output = string.Empty;
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        Log.Debug($"Querying table monlist for pathName {pathName}... ");
                        command.Parameters.Add("@pathname", System.Data.DbType.String).Value = pathName;
                        command.CommandText = "select pathexists from monlist where pathname = @pathName";
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                output = reader.GetValue(0).ToString();
                            }
                        }
                    }
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

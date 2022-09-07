using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;

namespace WinFIM.NET_Service
{
    internal class SQLiteHelper
    {
        public SQLiteHelper(string dbFile, string connectionString)
        {
            EnsureSQLiteDatabaseExists(dbFile, connectionString);
            EnsureSQLiteTablesExist(connectionString);
        }

        void EnsureSQLiteDatabaseExists(string dbFile, string connectionString)
        // Create the database if it doesn't exist
        {
            if (File.Exists(dbFile))
            {
                Console.WriteLine($"SqlLite database file {dbFile} already exists");
            }
            else
            {
                Console.WriteLine($"SqlLite databae file {dbFile} Does not exist. Creating");
                SQLiteConnection.CreateFile(dbFile);
            }
        }

        static void EnsureSQLiteTablesExist(string ConnectionString)
        // Ensure that all required tables exist
        {
            using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
            {
                try
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        Console.WriteLine("Creating Sqlite table baseline_table if it doesn't exist...");
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

                        Console.WriteLine("Creating Sqlite table conf_file_checksum if it doesn't exist...");
                        command.CommandText = @"
                            create table if not exists conf_file_checksum (
                                filename TEXT NOT NULL,
                                filehash TEXT
                            );
                        ";
                        command.ExecuteNonQuery();

                        Console.WriteLine("Creating Sqlite table current_table if it doesn't exist...");
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

                        Console.WriteLine("Creating Sqlite table monlist if it doesn't exist...");
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


        static void PopulateSQLiteTable(string ConnectionString)
        // To test SQL table population
        {
            using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
            {
                try
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        Console.WriteLine("Populating Sqlite table monlist...");
                        command.CommandText = @"
                        insert into monlist (pathname, pathexists) values ('c:\\test', 1);
                        insert into monlist (pathname, pathexists) values ('c:\\test2', 0)
                ";

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

        void QuerySQLiteTable(string ConnectionString)
        // To test SQL query
        {
            using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
            {
                try
                {
                    connection.Open();
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        Console.WriteLine("Querying Sqlite table monlist... ");
                        command.CommandText = "select * from monlist order by pathname";
                        using (SQLiteDataReader reader = command.ExecuteReader())
                            while (reader.Read())
                            {
                                Console.WriteLine($"pathname: {reader["pathname"]} pathexists: {reader["pathexists"]}");
                            }
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
        }
    }
}

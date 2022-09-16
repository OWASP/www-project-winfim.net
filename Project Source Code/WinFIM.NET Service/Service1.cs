using Serilog;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;

namespace WinFIM.NET_Service
{
    public partial class Service1 : ServiceBase
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int Wow64DisableWow64FsRedirection(ref IntPtr ptr);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int Wow64EnableWow64FsRedirection(ref IntPtr ptr);

        private readonly string _workDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private string ConnectionString { get; }

        private string DbFile { get; }

        public Service1()
        {
            InitializeComponent();
            eventLog1 = new EventLog();
            if (!EventLog.SourceExists("WinFIM.NET"))
            {
                EventLog.CreateEventSource("WinFIM.NET", "WinFIM.NET");
            }
            eventLog1.Source = "WinFIM.NET";
            eventLog1.Log = "WinFIM.NET";
            DbFile = _workDir + "\\fimdb.db";
            ConnectionString = @"URI=file:" + DbFile + ";PRAGMA journal_mode=WAL;";
        }

        // runs as a console application if a user interactively runs the "WinFIM.NET Service.exe" executable
        internal void TestStartupAndStop()
        {
            ServiceStart();
            this.OnStop();
        }

        protected override void OnStart(string[] args)
        {
            Thread myThread = new Thread(ServiceStart)
            {
                Name = "Worker Thread",
                IsBackground = true
            };
            myThread.Start();
        }

        private void ServiceStart()
        {
            //Read if there is any valid schedule timer (in minute)
            Initialise();
            string serviceStartMessage;
            string schedulerConf = _workDir + "\\scheduler.txt";
            int schedulerMin;
            try
            {
                string timerMinute = File.ReadLines(schedulerConf).First();
                timerMinute = timerMinute.Trim();
                schedulerMin = Convert.ToInt32(timerMinute);
            }
            catch (IOException e)
            {
                serviceStartMessage = "Please check if the file 'scheduler.txt' exists or a numeric value is input into the file 'scheduler.txt'.\nIt will run in continuous mode.";
                Log.Error($"Exception: {e.Message} - {serviceStartMessage}");
                eventLog1.WriteEntry($"Exception: {e.Message}\n{serviceStartMessage}", EventLogEntryType.Error, 7773); //setting the Event ID as 7773
                schedulerMin = 0;
            }

            if (schedulerMin > 0)
            //using timer mode
            {
                System.Timers.Timer timer = new System.Timers.Timer
                {
                    Interval = schedulerMin * 60000 // control the service to run every pre-defined minutes
                };
                timer.Elapsed += OnTimer;
                timer.Start();
                serviceStartMessage = Properties.Settings.Default.service_start_message + ": (UTC) " + DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt") + "\n\n";
                serviceStartMessage = serviceStartMessage + GetRemoteConnections() + "\nThis service will run every " + schedulerMin.ToString() + " minute(s).";
                Log.Debug(serviceStartMessage);
                eventLog1.WriteEntry(serviceStartMessage, EventLogEntryType.Information, 7771); //setting the Event ID as 7771
                FileIntegrityCheck();
            }
            else
            //run in continuous mode
            {
                serviceStartMessage = Properties.Settings.Default.service_start_message + ": (UTC) " + DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt") + "\n\n";
                serviceStartMessage = serviceStartMessage + GetRemoteConnections() + "\nThis service will run continuously.";
                Log.Debug(serviceStartMessage);
                eventLog1.WriteEntry(serviceStartMessage, EventLogEntryType.Information, 7771); //setting the Event ID as 7771
                bool trackerBoolean = true;
                while (trackerBoolean)
                {
                    trackerBoolean = FileIntegrityCheck();
                }
            }
        }

        private void OnTimer(object sender, ElapsedEventArgs args)
        {
            FileIntegrityCheck();
        }

        protected override void OnStop()
        {
            string serviceStopMessage = Properties.Settings.Default.service_stop_message + ": (UTC) " + DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt") + "\n\n";
            serviceStopMessage = serviceStopMessage + GetRemoteConnections() + "\n";
            Log.Information(serviceStopMessage);
            eventLog1.WriteEntry(serviceStopMessage, EventLogEntryType.Information, 7770); //setting the Event ID as 7770
        }

        //other functions

        //function for getting current remote connections mapping with users info on localhost
        private static string GetRemoteConnections()
        {
            string output = "ERROR in running CMD \"query user\"";
            try
            {
                using (Process process = new Process())
                {
                    IntPtr val = IntPtr.Zero;
                    _ = Wow64DisableWow64FsRedirection(ref val);
                    process.StartInfo.FileName = @"cmd.exe";
                    process.StartInfo.Arguments = "/c \"@echo off & @for /f \"tokens=1,2,3,4,5\" %A in ('netstat -ano ^| findstr ESTABLISHED ^| findstr /v 127.0.0.1') do (@for /f \"tokens=1,2,5\" %F in ('qprocess \"%E\"') do (@IF NOT %H==IMAGE @echo %A , %B , %C , %D , %E , %F , %G , %H))\"";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();
                    // Synchronously read the standard output of the spawned process. 
                    output = "Established Remote Connection (snapshot)" + "\n";
                    output = output + "========================================" + "\n" + "Proto | Local Address | Foreign Address | State | PID | USERNAME | SESSION NAME | IMAGE\n";
                    output += process.StandardOutput.ReadToEnd();
                    Log.Verbose(output);
                    process.WaitForExit();
                    _ = Wow64EnableWow64FsRedirection(ref val);
                    return output;
                }

            }
            catch (Exception e)
            {
                string errorMessage = $"Error in GetRemoteConnections : {e.Message}";
                Log.Error(errorMessage);
                return output + "\n" + e.Message;
            }

        }

        //get file owner information
        private static string GetFileOwner(string path)
        {
            try
            {
                string fileOwner = File.GetAccessControl(path).GetOwner(typeof(System.Security.Principal.NTAccount)).ToString();
                return fileOwner;
            }
            catch (Exception e)
            {
                string errorMessage = $"Error in GetFileOwner - {e.Message} for path: {path}";
                Log.Error(errorMessage);
                return "UNKNOWN";
            }
        }

        //get file size information (MB)
        private static string GetFileSize(string path)
        {
            try
            {
                long length = new FileInfo(path).Length;
                return Math.Round(Convert.ToDouble(length) / 1024 / 1024, 3).ToString(CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                string errorMessage = "GetFileSize error";
                Log.Error(e, errorMessage);
                return "UNKNOWN";
            }
        }

        //get file extension exclusion list and construct regex
        //the return string will be "EMPTY", if there is no file extension exclusion
        private string ExcludeExtensionRegex()
        {
            try
            {
                string extExcludeList = _workDir + "\\exclude_extension.txt";
                string[] lines = File.ReadAllLines(extExcludeList);
                lines = lines.Distinct().ToArray();
                List<string> extName = new List<string>();

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    string temp = line.TrimEnd('\r', '\n');

                    var match = Regex.Match(temp, @"/^[a-zA-Z0-9-_]+$/", RegexOptions.IgnoreCase);
                    //if the file extension does not match the exclusion
                    if (!match.Success)
                    {
                        Log.Verbose("Regex success: " + temp);
                        temp = "[.]" + temp;
                        extName.Add(temp);
                    }
                    else
                    {
                        string errorMessage = "Extension \"" + temp + "\" is invalid, file extension should be alphanumeric and '_' + '-' only.";
                        Log.Error(errorMessage);
                        eventLog1.WriteEntry(errorMessage, EventLogEntryType.Error, 7773); //setting the Event ID as 7773
                    }
                }

                bool isEmpty = !extName.Any();
                if (isEmpty)
                {
                    return "EMPTY";
                }
                else
                {
                    var result = String.Join("|", extName.ToArray());
                    string regex = "^.*(" + result + ")$";
                    return regex;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "ExcludeExtensionRegex");
                return "ERROR";
            }
        }

        //Compute file Hash in _sha256 return with a byte value.
        //for usage (return string of _sha256 file hash): BytesToString(GetHashSha256(filename))
        private readonly SHA256 _sha256 = SHA256.Create();
        private byte[] GetHashSha256(string filename)
        {

            Stream stream = new FileStream(filename, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite);
            byte[] tempResult = _sha256.ComputeHash(stream);
            stream.Close();
            return tempResult;
        }

        // Return a byte array as a sequence of hex values.
        private static string BytesToString(byte[] bytes)
        {
            string result = "";
            foreach (byte b in bytes) result += b.ToString("x2");
            return result;
        }

        private void Initialise()
        {
            SQLiteHelper.EnsureDatabaseExists(DbFile);
            SQLiteHelper.EnsureTablesExist(ConnectionString);

            string exExtHash = "";
            string exPathHash = "";
            string monHash = "";

            try
            {
                //create checksum for config files: exclude_extension.txt | exclude_path.txt | monlist.txt
                exExtHash = BytesToString(GetHashSha256(_workDir + "\\exclude_extension.txt"));
                exPathHash = BytesToString(GetHashSha256(_workDir + "\\exclude_path.txt"));
                monHash = BytesToString(GetHashSha256(_workDir + "\\monlist.txt"));
            }
            catch (Exception e)
            {
                string message = "Exception: " + e.Message + "\nConfig files: exclude_extension.txt | exclude_path.txt | monlist.txt is / are missing or having issue to access.";
                Log.Error(message);
                eventLog1.WriteEntry(message, EventLogEntryType.Error, 7773); //setting the Event ID as 7773
            }


            //compare the checksum with those stored in the DB
            try
            {
                //check if the baseline table is empty (If count is 0 then the table is empty.)
                string sql = "SELECT COUNT(*) FROM conf_file_checksum";
                string output = SQLiteHelper.ExecuteScalar(ConnectionString, sql)?.ToString() ?? "";
                Log.Verbose("Output count conf file hash: " + output);

                if (!output.Equals("3")) //suppose there should be 3 rows, if previous checksum exist
                {
                    try
                    {
                        //no checksum or incompetent checksum, empty all table
                        sql = "DELETE FROM conf_file_checksum";
                        SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);

                        sql = "DELETE FROM baseline_table";
                        SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);

                        sql = "DELETE FROM current_table";
                        SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);

                        //insert the current hash to DB
                        sql = "INSERT INTO conf_file_checksum (pathname, filehash) VALUES ('" + _workDir + "\\exclude_extension.txt','" + exExtHash + "')";
                        SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);

                        sql = "INSERT INTO conf_file_checksum (pathname, filehash) VALUES ('" + _workDir + "\\exclude_path.txt','" + exPathHash + "')";
                        SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);

                        sql = "INSERT INTO conf_file_checksum (pathname, filehash) VALUES ('" + _workDir + "\\monlist.txt','" + monHash + "')";
                        SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);
                    }
                    catch (Exception e)
                    {
                        string errorMessage = "SQLite Exception: " + e.Message;
                        Log.Error(errorMessage);
                    }
                }
                else
                {
                    //else compare the checksum, if difference, store the new checksum into DB, and empty both baseline_table and current_table
                    int count = 0;

                    sql = "SELECT filehash FROM conf_file_checksum WHERE pathname='" + _workDir + "\\exclude_extension.txt" + "'";
                    output = SQLiteHelper.ExecuteScalar(ConnectionString, sql)?.ToString() ?? "";
                    if (output.Equals(exExtHash))
                    {
                        count++;
                    }

                    sql = "SELECT filehash FROM conf_file_checksum WHERE pathname='" + _workDir + "\\exclude_path.txt" + "'";
                    output = SQLiteHelper.ExecuteScalar(ConnectionString, sql)?.ToString() ?? "";
                    if (output.Equals(exExtHash))
                    {
                        count++;
                    }

                    sql = "SELECT filehash FROM conf_file_checksum WHERE pathname='" + _workDir + "\\monlist.txt" + "'";
                    output = SQLiteHelper.ExecuteScalar(ConnectionString, sql)?.ToString() ?? "";
                    if (output.Equals(monHash))
                    {
                        count++;
                    }
                    Log.Verbose("Temp Same Count: " + count);

                    //if all hashes are the same
                    if (count == 3)
                    {
                        //use the same config
                    }
                    else
                    {
                        try
                        {
                            //clear all tables
                            sql = "DELETE FROM conf_file_checksum";
                            SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);

                            sql = "DELETE FROM baseline_table";
                            SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);

                            sql = "DELETE FROM current_table";
                            Log.Verbose(sql);
                            SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);

                            //insert the current hash to DB
                            sql = "INSERT INTO conf_file_checksum (pathname, filehash) VALUES ('" + _workDir +
                                  "\\exclude_extension.txt','" + exExtHash + "')";
                            SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);

                            sql = "INSERT INTO conf_file_checksum (pathname, filehash) VALUES ('" + _workDir +
                                  "\\exclude_path.txt','" + exPathHash + "')";
                            SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);

                            sql = "INSERT INTO conf_file_checksum (pathname, filehash) VALUES ('" + _workDir +
                                  "\\monlist.txt','" + monHash + "')";
                            SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);
                        }
                        catch (Exception e)
                        {
                            string errorMessage = "SQLite Exception: " + e.Message;
                            Log.Error(errorMessage);
                        }
                    }
                }

            }
            catch (Exception e)
            {
                Log.Error(e, e.Message);
                string sql = "DELETE FROM conf_file_checksum";
                try
                {
                    SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);
                }
                catch (Exception e1)
                {
                    string errorMessage = "SQLite Exception: " + e1.Message;
                    Log.Error(errorMessage);
                }
            }

        }

        private bool CheckIfBasePathExists(string line)
        {
            string sql = $"SELECT pathexists FROM monlist WHERE pathname = '{line}'";
            if (Directory.Exists(line) || File.Exists(line))
            {
                string output = SQLiteHelper.ExecuteScalar(ConnectionString, sql)?.ToString() ?? "";
                //if base path doesn't exist in SQLite table monlist
                if (string.IsNullOrEmpty(output))
                {
                    sql = $"INSERT OR REPLACE INTO monlist(pathname, pathexists, checktime) VALUES ('{line}', true, '{DateTime.UtcNow:M/d/yyyy hh:mm:ss tt}')";
                    SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);
                    string message = $"Base Path {line} exists - adding to monlist table";
                    Log.Warning(message);
                    eventLog1.WriteEntry(message, EventLogEntryType.Warning, 7776);
                }

                else if (output == "True")
                {
                    Log.Debug($"Base path {line} still exists");
                }
                return true;
            }
            else
            {
                string output = SQLiteHelper.ExecuteScalar(ConnectionString, sql)?.ToString() ?? "";
                if (string.IsNullOrEmpty(output))
                {
                    sql = $"INSERT OR REPLACE INTO monlist(pathname, pathexists, checktime) VALUES ('{line}', false, '{DateTime.UtcNow:M/d/yyyy hh:mm:ss tt}')";
                    SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);
                    string message = $"{line} does not exist";
                    Log.Warning(message);
                    eventLog1.WriteEntry(message, EventLogEntryType.Warning, 7778);
                }

                else if (output == "True")
                {
                    sql = $"INSERT OR REPLACE INTO monlist(pathname, pathexists, checktime) VALUES ('{line}', false, '{DateTime.UtcNow:M/d/yyyy hh:mm:ss tt}')";
                    SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);
                    string message = $"{line} has been deleted";
                    Log.Warning(message);
                    eventLog1.WriteEntry(message, EventLogEntryType.Warning, 7778);
                }
                else
                {
                    Log.Debug($"{line} still does not exist");
                }
                return false;
            }
        }

        private string[] GetFileMonList()
        {
            //read the monitoring list (line by line)
            string monListPath = _workDir + "\\monlist.txt";
            string[] monFileLines;
            try
            {
                monFileLines = File.ReadAllLines(monListPath);
            }
            catch (Exception e)
            {
                string errorMessage = "Exception : " + e.Message +
                                      "\nPlease make sure all input entries are correct under \"monlist.txt\".\nPlease restart the service after correction.";
                Log.Error(errorMessage);
                eventLog1.WriteEntry(errorMessage, EventLogEntryType.Error, 7773); //setting the Event ID as 7773
                throw;
            }
            return monFileLines;
        }

        private string[] GetFileList(string[] monFileLines)
        {
            string fileList = string.Empty;
            //get the full file mon list for further processing
            foreach (string line in monFileLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (!(CheckIfBasePathExists(line)))
                {
                    continue;
                }

                //1. check the line entry is a file or a directory
                FileAttributes attr = File.GetAttributes(line);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    //2. if it is a directory
                    //try to use cmd to get a full files (including hidden files) and all level of sub-directories list from a directory
                    using (Process process = new Process())
                    {
                        process.StartInfo.FileName = @"cmd.exe";
                        process.StartInfo.Arguments = @"/c dir /a: /b /s " + "\"" + line + "\"";
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.Start();
                        fileList = fileList + line + "\n"; //make sure the most outer directory is included in the list
                        fileList += process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                    }
                }
                else
                {
                    //3. if it is a file
                    fileList = fileList + line + "\n";
                }
            }

            //change all string in filelist to lowercase for easy comparison to exclusion list
            fileList = fileList.ToLower();
            fileList = fileList.TrimEnd('\r', '\n');
            Log.Verbose(fileList);

            //convert fileList string to string array
            string[] fileListArray = fileList.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.None
            );

            //remove duplicate elements in fileListArray
            fileListArray = fileListArray.Distinct().ToArray();

            return fileListArray;
        }

        private string[] GetFileExcludePath()
        {
            //read the exclude list (line by line)
            string excludePathFilePath = _workDir + "\\exclude_path.txt";
            string[] lines;
            try
            {
                lines = File.ReadAllLines(excludePathFilePath);
            }
            catch (Exception e)
            {
                string errorMessage = "Exception : " + e.Message +
                                      "\nPlease make sure all input entries are correct under \"exclude_path.txt\".\nPlease restart the service after correction.";
                Log.Error(errorMessage);
                eventLog1.WriteEntry(errorMessage, EventLogEntryType.Error, 7773); //setting the Event ID as 7773
                throw;
            }

            return lines;
        }

        private static string[] GetExcludeList(string[] lines)
        {
            string exFileList = string.Empty;
            //get the full exclude file list for further processing
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                try
                {
                    //1. check the line entry is a file or a directory
                    FileAttributes attr = File.GetAttributes(line);
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        //2. if it is a directory
                        //try to use cmd to get a full files (including hidden files) and all level of sub-directories list from a directory
                        using (Process process = new Process())
                        {
                            process.StartInfo.FileName = @"cmd.exe";
                            process.StartInfo.Arguments = @"/c dir /a: /b /s " + "\"" + line + "\"";
                            process.StartInfo.CreateNoWindow = true;
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.Start();
                            exFileList = exFileList + line + "\n"; //make sure the most outer directory is included in the list
                            exFileList += process.StandardOutput.ReadToEnd();
                            process.WaitForExit();
                        }
                    }
                    else
                    {
                        //3. if it is a file
                        exFileList = exFileList + line + "\n";
                    }

                }
                catch (Exception e)
                {
                    string errorMessage = "Exclusion error:" + e.Message;
                    Log.Error(errorMessage);
                    //The file path on the exclusion could be not exist
                }
            }
            //change all string in exFileList to lowercase for easy comparison to exclusion list
            exFileList = exFileList.ToLower();
            exFileList = exFileList.TrimEnd('\r', '\n');
            Log.Verbose($"exclude_path.txt contents: {exFileList}");

            //convert exFileList string to string array
            string[] exFileListArray = exFileList.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.None
            );
            //remove duplicate element in exFileListArray
            exFileListArray = exFileListArray.Distinct().ToArray();
            return exFileListArray;
        }

        private bool CheckBaseLine()
        {
            bool haveBaseline;
            try
            {
                string sql = "SELECT COUNT(*) FROM baseline_table";
                string output = SQLiteHelper.ExecuteScalar(ConnectionString, sql)?.ToString() ?? "";
                haveBaseline = !output.Equals("0");
                Log.Verbose($"Number of rows in baseline table: {output}");
            }
            catch (Exception e)
            {
                string errorMessage = $"Exception : {e.Message} \nPlease make sure local database file \"fimdb.db\" exists.";
                Log.Error(errorMessage);
                eventLog1.WriteEntry(errorMessage, EventLogEntryType.Error, 7773); //setting the Event ID as 7773
                return false;
            }
            return haveBaseline;
        }

        private void CheckDirectory(bool haveBaseline, string path)
        {
            //if there is content in baseline_table before, write to current_table
            if (haveBaseline)
            {
                string sql = "INSERT INTO current_table (pathname, filesize, fileowner, checktime, filehash, filetype) VALUES ('" + path + "',0,'" + GetFileOwner(path) + "','(UTC)" + DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt") + "','NA','Directory')";
                SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);

                //compare with baseline_table
                //1. check if the file exist in baseline_table
                sql = "SELECT COUNT(*) FROM baseline_table WHERE pathname='" + path + "'";
                string output = SQLiteHelper.ExecuteScalar(ConnectionString, sql).ToString();
                if (!output.Equals("0"))
                {
                    Log.Verbose("Directory :'" + path + "' has no change.");
                }
                else
                {
                    string message = "Directory :'" + path + "' is newly created.\nOwner: " + GetFileOwner(path);
                    Log.Warning(message);
                    eventLog1.WriteEntry(message, EventLogEntryType.Warning, 7776); //setting the Event ID as 7776
                }
            }
            //if there is no content in baseline_table, write to baseline_table instead
            else
            {
                string sql = "INSERT INTO baseline_table (pathname, filesize, fileowner, checktime, filehash, filetype) VALUES ('" + path + "',0,'" + GetFileOwner(path) + "','(UTC)" + DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt") + "','NA','Directory')";
                SQLiteHelper.ExecuteScalar(ConnectionString, sql);
            }
        }

        private void CheckFile(bool haveBaseline, string path)
        {
            string regex = ExcludeExtensionRegex();  //get the regex of file extension exclusion
            Log.Verbose("File Extension Exclusion REGEX:" + regex);
            SQLiteConnection connection = new SQLiteConnection(ConnectionString);
            SQLiteCommand command;
            SQLiteDataReader dataReader;
            string message;
            connection.Open();

            //a. if there is file extension exclusion
            string tempHash;
            if (regex.Equals("EMPTY"))
            {
                try
                {
                    tempHash = BytesToString(GetHashSha256(path));
                }
                catch (Exception e)
                {
                    tempHash = "UNKNOWN";
                    string errorMessage = $"File '{path}' is locked and not accessible for Hash calculation - {e.Message}.";
                    Log.Error(errorMessage);
                    eventLog1.WriteEntry(errorMessage, EventLogEntryType.Error, 7773); //setting the Event ID as 7773
                }
                //if there is content in baseline_table before, write to current_table
                if (haveBaseline)
                {
                    string sql = "INSERT INTO current_table (pathname, filesize, fileowner, checktime, filehash, filetype) VALUES ('" + path + "','" + GetFileSize(path) + "','" + GetFileOwner(path) + "','(UTC)" + DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt") + "','" + tempHash + "','File')";
                    SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);

                    //compare with baseline_table
                    //1. check if the file exist in baseline_table
                    sql = "SELECT COUNT(*) FROM baseline_table WHERE pathname='" + path + "'";
                    string output = SQLiteHelper.ExecuteScalar(ConnectionString, sql)?.ToString() ?? "";
                    if (!output.Equals("0"))
                    {
                        //1. check if the file hash in baseline_table changed
                        sql = "SELECT COUNT(*) FROM baseline_table WHERE pathname='" + path + "' AND filehash='" + tempHash + "'";
                        output = SQLiteHelper.ExecuteScalar(ConnectionString, sql)?.ToString() ?? "";
                        if (!output.Equals("0"))
                        {
                            Log.Verbose("File :'" + path + "' has no change.");
                        }
                        else
                        {
                            sql = "SELECT pathname, filesize, fileowner, filehash, checktime FROM baseline_table WHERE pathname='" + path + "'";
                            command = new SQLiteCommand(sql, connection);
                            dataReader = command.ExecuteReader();
                            if (dataReader.Read())
                            {
                                message = "File :'" + path + "' is modified. \nPrevious check at:" + dataReader.GetValue(4) + "\nFile hash: (Previous)" + dataReader.GetValue(3) + " (Current)" + tempHash + "\nFile Size: (Previous)" + dataReader.GetValue(1) + "MB (Current)" + GetFileSize(path) + "MB\nFile Owner: (Previous)" + dataReader.GetValue(2) + " (Current)" + GetFileOwner(path);
                                Log.Warning(message);
                                eventLog1.WriteEntry(message, EventLogEntryType.Warning, 7777); //setting the Event ID as 7777
                            }
                            dataReader.Close();
                            command.Dispose();
                        }
                    }
                    else
                    {
                        message = "File :'" + path + "' is newly created.\nOwner: " + GetFileOwner(path) + " Hash:" + tempHash;
                        Log.Warning(message);
                        eventLog1.WriteEntry("File :'" + path + "' is newly created.\nOwner: " + GetFileOwner(path) + " Hash:" + tempHash, EventLogEntryType.Warning, 7776); //setting the Event ID as 7776
                    }
                }
                //if there is no content in baseline_table, write to baseline_table instead
                else
                {
                    string sql = "INSERT INTO baseline_table (pathname, filesize, fileowner, checktime, filehash, filetype) VALUES ('" + path + "'," + GetFileSize(path) + ",'" + GetFileOwner(path) + "','(UTC)" + DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt") + "','" + tempHash + "','File')";
                    Log.Verbose(sql);
                    try
                    {
                        SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);
                    }
                    catch (Exception e)
                    {
                        string errorMessage = "SQLite Exception: " + e.Message;
                        Log.Error(errorMessage);
                    }
                }
            }
            //b. if there is file extension exclusion
            else
            {
                var match = Regex.Match(path, regex, RegexOptions.IgnoreCase);
                //if the file extension does not match the exclusion
                if (!match.Success)
                {
                    try
                    {
                        tempHash = BytesToString(GetHashSha256(path));
                    }
                    catch (Exception e)
                    {
                        tempHash = "UNKNOWN";
                        message = $"File '{path}' is locked and not accessible for Hash calculation - {e.Message}.";
                        Log.Error(message);
                        eventLog1.WriteEntry(message, EventLogEntryType.Error, 7773); //setting the Event ID as 7773
                    }
                    if (haveBaseline)
                    {
                        string sql = "INSERT INTO current_table (pathname, filesize, fileowner, checktime, filehash, filetype) VALUES ('" + path + "'," + GetFileSize(path) + ",'" + GetFileOwner(path) + "','(UTC)" + DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt") + "','" + tempHash + "','File')";
                        try
                        {
                            SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);
                        }
                        catch (Exception e)
                        {
                            message = "SQLite Exception: " + e.Message;
                            Log.Error(message);
                        }

                        //compare with baseline_table
                        //1. check if the file exist in baseline_table
                        sql = "SELECT COUNT(*) FROM baseline_table WHERE pathname='" + path + "'";
                        string output = SQLiteHelper.ExecuteScalar(ConnectionString, sql)?.ToString() ?? "";
                        if (!output.Equals("0"))
                        {
                            //1. check if the file hash in baseline_table changed
                            sql = "SELECT COUNT(*) FROM baseline_table WHERE pathname='" + path + "' AND filehash='" + tempHash + "'";
                            output = SQLiteHelper.ExecuteScalar(ConnectionString, sql)?.ToString() ?? "";
                            if (!output.Equals("0"))
                            {
                                Log.Verbose("File :'" + path + "' has no change.");
                            }
                            else
                            {
                                sql = "SELECT pathname, filesize, fileowner, filehash, checktime FROM baseline_table WHERE pathname='" + path + "'";
                                command = new SQLiteCommand(sql, connection);
                                dataReader = command.ExecuteReader();
                                if (dataReader.Read())
                                {
                                    message = "File :'" + path + "' is modified. Previous check at:" + dataReader.GetValue(4) + "\nFile hash: (Previous)" + dataReader.GetValue(3) + " (Current)" + tempHash + "\nFile Size: (Previous)" + dataReader.GetValue(1) + "MB (Current)" + GetFileSize(path) + "MB\nFile Owner: (Previous)" + dataReader.GetValue(2) + " (Current)" + GetFileOwner(path);
                                    Log.Warning(message);
                                    eventLog1.WriteEntry(message, EventLogEntryType.Warning, 7777); //setting the Event ID as 7777
                                }
                                dataReader.Close();
                                command.Dispose();
                            }
                        }
                        else
                        {
                            message = "File :'" + path + "' is newly created.\nOwner: " + GetFileOwner(path) + " Hash:" + tempHash;
                            Log.Warning(message);
                            eventLog1.WriteEntry(message, EventLogEntryType.Warning, 7776); //setting the Event ID as 7776
                        }
                    }
                    //if there is no content in baseline_table, write to baseline_table instead
                    else
                    {
                        string sql = "INSERT INTO baseline_table (pathname, filesize, fileowner, checktime, filehash, filetype) VALUES ('" + path + "'," + GetFileSize(path) + ",'" + GetFileOwner(path) + "','(UTC)" + DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt") + "','" + tempHash + "','File')";
                        try
                        {
                            SQLiteHelper.ExecuteNonQuery(ConnectionString, sql);
                        }
                        catch (Exception e)
                        {
                            string errorMessage = "SQLite Exception: " + e.Message;
                            Log.Error(errorMessage);
                        }
                    }
                }
            }
            //local database close connection
            connection.Close();
        }

        private void CheckIfDeleted(bool haveBaseline)
        {
            SQLiteConnection con2 = new SQLiteConnection(ConnectionString);
            SQLiteCommand command2;
            SQLiteDataReader dataReader2;
            string sql2;
            string output2;
            con2.Open();

            if (haveBaseline)
            {
                sql2 = "SELECT baseline_table.pathname FROM baseline_table LEFT JOIN current_table ON baseline_table.pathname = current_table.pathname WHERE current_table.pathname IS NULL";
                command2 = new SQLiteCommand(sql2, con2);
                dataReader2 = command2.ExecuteReader();
                while (dataReader2.Read())
                {
                    output2 = dataReader2.GetValue(0).ToString();
                    string deletedMessage = "The file / directory '" + output2 + "' is deleted.";
                    Log.Warning(deletedMessage);
                    eventLog1.WriteEntry(deletedMessage, EventLogEntryType.Warning, 7778); //setting the Event ID as 7778
                }
                dataReader2.Close();
                //delete all rows in baseline_table, copy all rows from current_table to baseline_table, then clear current_table
                sql2 = "DELETE FROM baseline_table";
                command2 = new SQLiteCommand(sql2, con2);
                try
                {
                    command2.ExecuteNonQuery();
                    command2.Dispose();
                }
                catch (Exception e)
                {
                    string errorMessage = "SQLite Exception: " + e.Message;
                    Log.Error(errorMessage);
                }

                sql2 = "INSERT INTO baseline_table SELECT * FROM current_table";
                command2 = new SQLiteCommand(sql2, con2);
                try
                {
                    command2.ExecuteNonQuery();
                    command2.Dispose();
                }
                catch (Exception e)
                {
                    string errorMessage = "SQLite Exception: " + e.Message;
                    Log.Error(errorMessage);
                }

                sql2 = "DELETE FROM current_table";
                command2 = new SQLiteCommand(sql2, con2);
                try
                {
                    command2.ExecuteNonQuery();
                    command2.Dispose();
                }
                catch (Exception e)
                {
                    string errorMessage = "SQLite Exception: " + e.Message;
                    Log.Error(errorMessage);
                }

            }
            con2.Close();
        }

        private bool FileIntegrityCheck()
        {
            Log.Information("Starting checks");

            bool haveBaseline = CheckBaseLine(); //check if there is already data in the baseline table from a previous FIM check

            Stopwatch watch = new Stopwatch();
            watch.Start();

            try
            {
                string[] monListFileLines = GetFileMonList(); //get the list of paths in the monlist.txt file

                string[] pathList = GetFileList(monListFileLines); //get the list of files to watch

                string[] excludePathLines = GetFileExcludePath(); //get the list of paths in the exclude_path.txt file

                string[] excludePathList = GetExcludeList(excludePathLines);

                IEnumerable<string> finalPathList = pathList.Except(excludePathList); //filter exclusion file list

                foreach (string path in finalPathList)
                {
                    Log.Verbose($"Checking path {path}");
                    try
                    {
                        //1. check the line entry is a file or a directory
                        FileAttributes attr = File.GetAttributes(path);
                        if (attr.HasFlag(FileAttributes.Directory))
                        {
                            CheckDirectory(haveBaseline, path);
                        }
                        else
                        {
                            CheckFile(haveBaseline, path);
                        }
                    }
                    catch (Exception e)
                    {
                        string errorMessage = $"File '{path}' could be renamed / deleted during the hash calculation. This file is ignored in this checking cycle - {e.Message}.";
                        Log.Error(errorMessage);
                        eventLog1.WriteEntry(errorMessage, EventLogEntryType.Error, 7773); //setting the Event ID as 7773
                    }

                }

                CheckIfDeleted(haveBaseline);

                watch.Stop();
                string stopMessage = "Total time consumed in this round file integrity checking  = " + watch.ElapsedMilliseconds + "ms (" + Math.Round(Convert.ToDouble(watch.ElapsedMilliseconds) / 1000, 3).ToString(CultureInfo.InvariantCulture) + "s).\n" + GetRemoteConnections();
                Log.Debug(stopMessage);
                eventLog1.WriteEntry(stopMessage, EventLogEntryType.Information, 7771); //setting the Event ID as 7771
                Log.Verbose("Total time consumed in this round file integrity checking  = " + watch.ElapsedMilliseconds + "ms (" + Math.Round(Convert.ToDouble(watch.ElapsedMilliseconds) / 1000, 3).ToString(CultureInfo.InvariantCulture) + "s).");
                return true;
            }
            catch (Exception e)
            {
                string errorMessage = "Exception : " + e.Message + "\nPlease make sure all input entries are correct under \"monlist.txt\", \"exclude_path.txt\" and \"exclude_extension.txt\".\nPlease restart the service after correction.";
                Log.Error(errorMessage);
                eventLog1.WriteEntry(errorMessage, EventLogEntryType.Error, 7773); //setting the Event ID as 7773
                return false;
            }
        }
    }
}

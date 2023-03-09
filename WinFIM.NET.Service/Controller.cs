using Serilog;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace WinFIM.NET.Service
{
    [SupportedOSPlatform("windows")]
    public class Controller
    {
        private readonly ConfigurationOptions _configurationOptions;
        private readonly LogHelper _logHelper;

        public Controller(ConfigurationOptions configurationOptions, LogHelper logHelper)
        {
            _configurationOptions = configurationOptions;
            _logHelper = logHelper;
            SQLiteHelper1 = new SQLiteHelper();
        }

        private SQLiteHelper SQLiteHelper1 { get; set; }

        //get file owner information
        private static string GetFileOwner(string path)
        {
            string fileOwner;
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!(File.Exists(path)))
            {
                fileOwner = $"File not found: {path}";
                return fileOwner;
            }
            try
            {
                var ac = new FileInfo(path).GetAccessControl();
                fileOwner = new FileInfo(path).GetAccessControl().GetOwner(typeof(System.Security.Principal.NTAccount))?.ToString() ?? throw new InvalidOperationException();
                //fileOwner = File.GetAccessControl(path).GetOwner(typeof(System.Security.Principal.NTAccount)).ToString();
            }
            catch
            {
                try
                {
                    fileOwner = new FileInfo(path).GetAccessControl().GetOwner(typeof(System.Security.Principal.SecurityIdentifier))?.ToString() ?? throw new InvalidOperationException();
                    //fileOwner = File.GetAccessControl(path).GetOwner(typeof(System.Security.Principal.SecurityIdentifier)).ToString();
                }
                catch (Exception e)
                {
                    string errorMessage = $"Error in GetFileOwner - {e.Message} for path: {path}";
                    Console.WriteLine(errorMessage);
                    fileOwner = "UNKNOWN";
                }
            }
            return fileOwner;
        }

        //get directory owner information
        private static string? GetDirectoryOwner(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            string? directoryOwner;
            try
            {
                if (!(Directory.Exists(path)))
                {
                    directoryOwner = $"Directory not found: {path}";
                    return directoryOwner;
                }
                var dac = new DirectoryInfo(path).GetAccessControl();
                directoryOwner = new DirectoryInfo(path).GetAccessControl().GetOwner(typeof(System.Security.Principal.NTAccount))?.ToString();
                //directoryOwner = Directory.GetAccessControl(path).GetOwner(typeof(System.Security.Principal.NTAccount)).ToString();
            }
            catch
            {
                try
                {
                    directoryOwner = new DirectoryInfo(path).GetAccessControl().GetOwner(typeof(System.Security.Principal.SecurityIdentifier))?.ToString();
                    //directoryOwner = Directory.GetAccessControl(path).GetOwner(typeof(System.Security.Principal.SecurityIdentifier)).ToString();
                }
                catch (Exception e)
                {
                    string errorMessage = $"Error in GetDirectoryOwner - {e.Message} for path: {path}";
                    Console.WriteLine(errorMessage);
                    directoryOwner = "UNKNOWN";
                }
            }
            return directoryOwner;
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
                Log.Error(e, "GetFileSize error");
                return "UNKNOWN";
            }
        }

        //get file extension exclusion list and construct regex
        //the return string will be "EMPTY", if there is no file extension exclusion
        private string ExcludeExtensionRegex()
        {
            try
            {
                string excludeExtensionPath = LogHelper.WorkDir + "\\exclude_extension.txt";
                string[] lines = File.ReadAllLines(excludeExtensionPath);
                lines = lines.Distinct().ToArray();
                List<string> extName = new();

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
                        _logHelper.WriteEventLog(errorMessage, EventLogEntryType.Error, 7773);
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

        public void Initialise()
        {
            SQLiteHelper1.EnsureDatabaseExists();
            string exExtHash = "";
            string exPathHash = "";
            string monHash = "";

            try
            {
                //create checksum for config files: exclude_extension.txt | exclude_path.txt | monlist.txt
                exExtHash = BytesToString(GetHashSha256(LogHelper.WorkDir + "\\exclude_extension.txt"));
                exPathHash = BytesToString(GetHashSha256(LogHelper.WorkDir + "\\exclude_path.txt"));
                monHash = BytesToString(GetHashSha256(LogHelper.WorkDir + "\\monlist.txt"));
            }
            catch (Exception e)
            {
                string message = "Exception: " + e.Message + "\nConfig files: exclude_extension.txt | exclude_path.txt | monlist.txt is / are missing or having issue to access.";
                Log.Error(message);
                _logHelper.WriteEventLog(message, EventLogEntryType.Error, 7773);
            }

            //compare the checksum with those stored in the DB
            try
            {
                //check if the baseline table is empty (If count is 0 then the table is empty.)
                string sql = "SELECT COUNT(*) FROM CONF_FILE_CHECKSUM";
                string output = SQLiteHelper1.ExecuteScalar(sql)?.ToString() ?? "";
                Log.Verbose("Output count conf file hash: " + output);

                if (!output.Equals("3")) //suppose there should be 3 rows, if previous checksum exist
                {
                    try
                    {
                        //no checksum or incompetent checksum, empty all table
                        sql = "DELETE FROM CONF_FILE_CHECKSUM";
                        SQLiteHelper1.ExecuteNonQuery(sql);

                        sql = "DELETE FROM BASELINE_PATH";
                        SQLiteHelper1.ExecuteNonQuery(sql);

                        sql = "DELETE FROM CURRENT_PATH";
                        SQLiteHelper1.ExecuteNonQuery(sql);

                        //insert the current hash to DB
                        sql = $"INSERT INTO CONF_FILE_CHECKSUM (pathname, filehash) VALUES ('{LogHelper.WorkDir}\\exclude_extension.txt','{exExtHash}')";
                        SQLiteHelper1.ExecuteNonQuery(sql);

                        sql = $"INSERT INTO CONF_FILE_CHECKSUM (pathname, filehash) VALUES ('{LogHelper.WorkDir}\\exclude_path.txt','{exPathHash}')";
                        SQLiteHelper1.ExecuteNonQuery(sql);

                        sql = $"INSERT INTO CONF_FILE_CHECKSUM (pathname, filehash) VALUES ('{LogHelper.WorkDir}\\monlist.txt','{monHash}')";
                        SQLiteHelper1.ExecuteNonQuery(sql);
                    }
                    catch (Exception e)
                    {
                        string errorMessage = "SQLite Exception: " + e.Message;
                        Log.Error(errorMessage);
                    }
                }
                else
                {
                    //else compare the checksum, if difference, store the new checksum into DB, and empty both BASELINE_PATH and CURRENT_PATH
                    int count = 0;

                    sql = $"SELECT filehash FROM CONF_FILE_CHECKSUM WHERE pathname='{LogHelper.WorkDir}\\exclude_extension.txt'";
                    output = SQLiteHelper1.ExecuteScalar(sql)?.ToString() ?? "";
                    if (output.Equals(exExtHash))
                    {
                        count++;
                    }

                    sql = $"SELECT filehash FROM CONF_FILE_CHECKSUM WHERE pathname='{LogHelper.WorkDir}\\exclude_path.txt'";
                    output = SQLiteHelper1.ExecuteScalar(sql)?.ToString() ?? "";
                    if (output.Equals(exExtHash))
                    {
                        count++;
                    }

                    sql = $"SELECT filehash FROM CONF_FILE_CHECKSUM WHERE pathname='{LogHelper.WorkDir}\\monlist.txt'";
                    output = SQLiteHelper1.ExecuteScalar(sql)?.ToString() ?? "";
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
                            sql = "DELETE FROM CONF_FILE_CHECKSUM";
                            SQLiteHelper1.ExecuteNonQuery(sql);

                            sql = "DELETE FROM BASELINE_PATH";
                            SQLiteHelper1.ExecuteNonQuery(sql);

                            sql = "DELETE FROM CURRENT_PATH";
                            Log.Verbose(sql);
                            SQLiteHelper1.ExecuteNonQuery(sql);

                            //insert the current hash to DB
                            sql = $"INSERT INTO CONF_FILE_CHECKSUM (pathname, filehash) VALUES ('{LogHelper.WorkDir}\\exclude_extension.txt','{exExtHash}')";
                            SQLiteHelper1.ExecuteNonQuery(sql);

                            sql = $"INSERT INTO CONF_FILE_CHECKSUM (pathname, filehash) VALUES ('{LogHelper.WorkDir}\\exclude_path.txt','{exPathHash}')";
                            SQLiteHelper1.ExecuteNonQuery(sql);

                            sql = $"INSERT INTO CONF_FILE_CHECKSUM (pathname, filehash) VALUES ('{LogHelper.WorkDir}\\monlist.txt','{monHash}')";
                            SQLiteHelper1.ExecuteNonQuery(sql);
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
                string sql = "DELETE FROM CONF_FILE_CHECKSUM";
                try
                {
                    SQLiteHelper1.ExecuteNonQuery(sql);
                }
                catch (Exception e1)
                {
                    string errorMessage = "SQLite Exception: " + e1.Message;
                    Log.Error(errorMessage);
                }
            }
            finally
            {
                SQLiteHelper1.Dispose();
            }

        }

        private bool CheckIfMonListBasePathExists(string path, bool haveBaseLinePath)
        {
            if (Directory.Exists(path) || File.Exists(path))
            {
                return true; // we just need to know that the base path exists - the CheckPath method later on will compare hashes
            }
            else if (haveBaseLinePath)
            {
                string sql = $"SELECT COUNT(*) FROM BASELINE_PATH WHERE pathname='{path}'";
                string output = SQLiteHelper1.ExecuteScalar(sql)?.ToString() ?? "";
                if (!output.Equals("0"))
                {
                    string operation = "deleted";
                    string sourceFile = "monlist.txt";
                    string target = "Base path";
                    Log.Warning("{target} from {sourceFile}:{path} has been {operation}", target, sourceFile, path, operation);
                    string eventLogMessage = $"{target} from {sourceFile}:{path} has been {operation}";
                    _logHelper.WriteEventLog(eventLogMessage, EventLogEntryType.Warning, 7778);
                }
            }
            return false;
        }

        private string[] GetFileMonList()
        {
            //read the monitoring list (line by line)
            string monListPath = LogHelper.WorkDir + "\\monlist.txt";
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
                _logHelper.WriteEventLog(errorMessage, EventLogEntryType.Error, 7773);
                throw;
            }
            return monFileLines;
        }

        private string[] GetPathList(string[] monFileLines, bool haveBaseLine)
        {
            var fileList = new List<string>();
            //get the full file mon list for further processing
            foreach (string line in monFileLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (!(CheckIfMonListBasePathExists(line, haveBaseLine)))
                {
                    continue;
                }

                var fileOrDirectory = new FileInfo(line);

                if (fileOrDirectory.Attributes.HasFlag(FileAttributes.Directory))
                {
                    var files = GetFiles(line);
                    fileList.AddRange(files);
                }
                else
                {
                    fileList.Add(fileOrDirectory.FullName);
                }
            }

            //change all string in filelist to lowercase for easy comparison to exclusion list
            //remove duplicate elements in fileListArray
            var fileListArray = fileList
                .Select(x => x.ToLowerInvariant())
                .Distinct()
                .ToArray();

            return fileListArray;
        }

        private string[] GetFileExcludePath()
        {
            //read the exclude list (line by line)
            string excludePathFilePath = LogHelper.WorkDir + "\\exclude_path.txt";
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
                _logHelper.WriteEventLog(errorMessage, EventLogEntryType.Error, 7773);
                throw;
            }

            return lines;
        }

        private static string[] GetExcludeList(string[] lines)
        {
            var exFileList = new List<string>();
            //get the full exclude file list for further processing
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                try
                {
                    var fileOrDirectory = new FileInfo(line);
                    if (fileOrDirectory.Attributes.HasFlag(FileAttributes.Directory))
                    {
                        var files = GetFiles(line);
                        exFileList.AddRange(files);
                    }
                    else
                    {
                        exFileList.Add(fileOrDirectory.FullName);
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

            var exFileListArray = exFileList
                .Select(x => x.ToLowerInvariant())
                .Distinct()
                .ToArray();

            return exFileListArray;
        }

        private static ICollection<string> GetFiles(string path)
        {
            var files = new List<string>();
            string[] directories = Array.Empty<string>();
            try
            {
                files.AddRange(Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly));
                directories = Directory.GetDirectories(path);
            }
            // Ignore inaccessible paths
            catch (UnauthorizedAccessException) { }

            foreach (var directory in directories)
            {
                try
                {
                    files.AddRange(GetFiles(directory));
                }
                // Ignore inaccessible paths
                catch (UnauthorizedAccessException) { }
            }

            return files;
        }

        private bool CheckBaseLine()
        {
            bool haveBaseline;
            try
            {
                string sql = "SELECT COUNT(*) FROM BASELINE_PATH";
                string output = SQLiteHelper1.ExecuteScalar(sql)?.ToString() ?? "";
                haveBaseline = !output.Equals("0");
                Log.Verbose($"Number of rows in table BASELINE_PATH: {output}");
            }
            catch (Exception e)
            {
                string errorMessage = $"Exception : {e.Message} \nPlease make sure local database file \"fimdb.db\" exists.";
                Log.Error(errorMessage);
                _logHelper.WriteEventLog(errorMessage, EventLogEntryType.Error, 7773);
                return false;
            }
            return haveBaseline;
        }

        private void CheckPath(bool haveBaseLinePath, string path, int attempt = 0)
        {
            Log.Debug($"Checking path {path}");
            try
            {
                attempt++;
                //1. check the line entry is a file or a directory
                FileAttributes attr = File.GetAttributes(path);
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    CheckDirectory(haveBaseLinePath, path);
                }
                else
                {
                    CheckFile(haveBaseLinePath, path);
                }
            }
            catch (Exception e)
            {
                if (attempt < 2)
                {
                    Thread.Sleep(500);
                    CheckPath(haveBaseLinePath, path, attempt);
                }
                else
                {
                    string pathType = "file";
                    string likelyReason= "may have been renamed / deleted during the hash calculation";
                    string mitigation = "This file is ignored in this checking cycle";
                    string errorMessage = e.Message;
                    Log.Error("{pathType} {path} {likelyReason}. {mitigation} - {errorMessage}", pathType, path, likelyReason, mitigation, errorMessage);
                    string eventLogMessage = $"{pathType} {path} {likelyReason}. {mitigation} - {errorMessage}";
                    _logHelper.WriteEventLog(eventLogMessage, EventLogEntryType.Error,7773);
                }
            }
        }

        private void CheckDirectory(bool haveBaseLinePath, string path)
        {
            string? directoryOwner = GetDirectoryOwner(path);
            //if there is content in BASELINE_PATH before, write to CURRENT_PATH
            if (haveBaseLinePath)
            {
                string sql = "INSERT INTO CURRENT_PATH (pathname, pathexists, filesize, owner, checktime, filehash, pathtype) " +
                             $"VALUES ('{path}',true,0,'{directoryOwner}','(UTC){DateTime.UtcNow:yyyy/MM/dd hh:mm:ss tt}','NA','Directory')";
                SQLiteHelper1.ExecuteNonQuery(sql);

                //compare with BASELINE_PATH
                //1. check if the file exist in BASELINE_PATH
                sql = $"SELECT COUNT(*) FROM BASELINE_PATH WHERE pathname='{path}'";
                string output = SQLiteHelper1.ExecuteScalar(sql)?.ToString() ?? throw new InvalidOperationException();
                if (!output.Equals("0"))
                {
                    Log.Verbose($"Directory: '{path}' has no change.");
                }
                else
                {
                    string pathType = "directory";
                    string operation = "created";
                    Log.Warning("{pathType}: '{path}' is {operation}. Owner: {directoryOwner}", pathType, path, operation, directoryOwner);
                    string eventLogMessage = $"{pathType}: '{path}' is {operation}. Owner: {directoryOwner}";
                    _logHelper.WriteEventLog(eventLogMessage, EventLogEntryType.Warning, 7776);
                }
            }
            //if there is no content in BASELINE_PATH, write to BASELINE_PATH instead
            else
            {
                string sql = "INSERT INTO BASELINE_PATH (pathname, pathexists, filesize, owner, checktime, filehash, pathtype) " +
                             $"VALUES ('{path}',true,0,'{directoryOwner}','(UTC){DateTime.UtcNow:yyyy/MM/dd hh:mm:ss tt}','NA','Directory')";
                SQLiteHelper1.ExecuteScalar(sql);
                Log.Debug($"Directory {path} exists");
            }
        }

        private void CheckFile(bool haveBaseLinePath, string path)
        {
            string regex = ExcludeExtensionRegex();  //get the regex of file extension exclusion
            string fileOwner = GetFileOwner(path);
            Log.Verbose("File Extension Exclusion REGEX:" + regex);
            SQLiteCommand command;
            SQLiteDataReader dataReader;
            string message;

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
                    _logHelper.WriteEventLog(errorMessage, EventLogEntryType.Error, 7773);
                }
                //if there is content in BASELINE_PATH before, write to CURRENT_PATH
                if (haveBaseLinePath)
                {
                    string sql = "INSERT INTO CURRENT_PATH (pathname, pathexists, filesize, owner, checktime, filehash, pathtype) " +
                                 $"VALUES ('{path}',true,'{GetFileSize(path)}','{fileOwner}','(UTC){DateTime.UtcNow:yyyy/MM/dd hh:mm:ss tt}','{tempHash}','File'";
                    SQLiteHelper1.ExecuteNonQuery(sql);

                    //compare with BASELINE_PATH
                    //1. check if the file exist in BASELINE_PATH
                    sql = $"SELECT COUNT(*) FROM BASELINE_PATH WHERE pathname='{path}'";
                    string output = SQLiteHelper1.ExecuteScalar(sql)?.ToString() ?? "";
                    if (!output.Equals("0"))
                    {
                        //1. check if the file hash in BASELINE_PATH changed
                        sql = $"SELECT COUNT(*) FROM BASELINE_PATH WHERE pathname='{path}' AND filehash='{tempHash}'";
                        output = SQLiteHelper1.ExecuteScalar(sql)?.ToString() ?? "";
                        if (!output.Equals("0"))
                        {
                            Log.Verbose($"File: '{path}' has no change.");
                        }
                        else
                        {
                            sql = "SELECT pathname, pathexists, filesize, owner, filehash, checktime FROM BASELINE_PATH WHERE pathname=@path";
                            command = new(sql, SQLiteHelper1.Connection);
                            command.Parameters.Add("@path", DbType.String).Value = path;
                            dataReader = command.ExecuteReader();
                            if (dataReader.Read())
                            {
                                string? previousCheck = dataReader.GetValue(5).ToString();
                                string? hashPrevious = dataReader.GetValue(4).ToString();
                                string? sizePrevious = dataReader.GetValue(2).ToString();
                                string sizeCurrent = GetFileSize(path);
                                string? ownerPrevious = dataReader.GetValue(3).ToString();
                                string operation = "modified";

                                Log.Warning("File: {path} is {operation}. Previous check at:{previousCheck}. Hash: (Previous){hashPrevious} (Current){tempHash}. Size: (Previous) {sizePrevious}MB (Current){sizeCurrent}MB File Owner: (Previous){ownerPrevious} (Current){fileOwner}", path, operation, previousCheck, hashPrevious, tempHash, sizePrevious, sizeCurrent, ownerPrevious, fileOwner);
                                string eventLogMessage = $"File: {path} is modified. Previous check at:{previousCheck}. Hash: (Previous){hashPrevious} (Current){tempHash}. Size: (Previous) {sizePrevious}MB (Current){sizeCurrent}MB File Owner: (Previous){ownerPrevious} (Current){fileOwner}";
                                _logHelper.WriteEventLog(eventLogMessage, EventLogEntryType.Warning, 7777);
                            }
                            dataReader.Close();
                            command.Dispose();
                        }
                    }
                    else
                    {
                        string pathType = "file";
                        string operation = "created";
                        Log.Warning("{pathType}: '{path}' is {operation}. Owner: {fileOwner}. Hash: {tempHash}", pathType, path, operation, fileOwner, tempHash);
                        string eventLogMessage = $"{pathType}: '{path}' is {operation}. Owner: {fileOwner}. Hash: {tempHash}";
                        _logHelper.WriteEventLog(eventLogMessage, EventLogEntryType.Warning, 7776);
                    }
                }
                //if there is no content in BASELINE_PATH, write to BASELINE_PATH instead
                else
                {
                    string sql = "INSERT INTO BASELINE_PATH (pathname, pathexists, filesize, owner, checktime, filehash, pathtype) " +
                                 $"VALUES ('{path}',true,{GetFileSize(path)},'{fileOwner}','(UTC){DateTime.UtcNow:yyyy/MM/dd hh:mm:ss tt}','{tempHash}','File')";
                    Log.Verbose(sql);
                    try
                    {
                        SQLiteHelper1.ExecuteNonQuery(sql);
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
                        _logHelper.WriteEventLog(message, EventLogEntryType.Error, 7773);
                    }
                    if (haveBaseLinePath)
                    {
                        string sql = "INSERT INTO CURRENT_PATH (pathname, pathexists, filesize, owner, checktime, filehash, pathtype) " +
                                     $"VALUES ('{path}',true,{GetFileSize(path)},'{fileOwner}','(UTC){DateTime.UtcNow:yyyy/MM/dd hh:mm:ss tt}','{tempHash}','File')";
                        try
                        {
                            SQLiteHelper1.ExecuteNonQuery(sql);
                        }
                        catch (Exception e)
                        {
                            message = "SQLite Exception: " + e.Message;
                            Log.Error(message);
                        }

                        //compare with BASELINE_PATH
                        //1. check if the file exist in BASELINE_PATH
                        sql = $"SELECT COUNT(*) FROM BASELINE_PATH WHERE pathname='{path}'";
                        string output = SQLiteHelper1.ExecuteScalar(sql)?.ToString() ?? "";
                        if (!output.Equals("0"))
                        {
                            //1. check if the file hash in BASELINE_PATH changed
                            sql = $"SELECT COUNT(*) FROM BASELINE_PATH WHERE pathname='{path}' AND filehash='{tempHash}'";
                            output = SQLiteHelper1.ExecuteScalar(sql)?.ToString() ?? "";
                            if (!output.Equals("0"))
                            {
                                Log.Verbose($"File: '{path}' has no change.");
                            }
                            else
                            {
                                sql = "SELECT pathname, pathexists, filesize, owner, filehash, checktime FROM BASELINE_PATH WHERE pathname=@path";
                                command = new(sql, SQLiteHelper1.Connection);
                                command.Parameters.Add("@path", DbType.String).Value = path;
                                dataReader = command.ExecuteReader();
                                if (dataReader.Read())
                                {
                                    string? previousCheck = dataReader.GetValue(5).ToString();
                                    string? hashPrevious = dataReader.GetValue(4).ToString();
                                    string? sizePrevious = dataReader.GetValue(2).ToString();
                                    string sizeCurrent = GetFileSize(path);
                                    string? ownerPrevious = dataReader.GetValue(3).ToString();
                                    string operation = "modified";

                                    Log.Warning("File: {path} is {operation}. Previous check at:{previousCheck}. Hash: (Previous){hashPrevious} (Current){tempHash}. Size: (Previous) {sizePrevious}MB (Current){sizeCurrent}MB File Owner: (Previous){ownerPrevious} (Current){fileOwner}", path, operation, previousCheck, hashPrevious, tempHash, sizePrevious, sizeCurrent, ownerPrevious, fileOwner);
                                    string eventLogMessage = $"File: {path} is modified. Previous check at:{previousCheck}. Hash: (Previous){hashPrevious} (Current){tempHash}. Size: (Previous) {sizePrevious}MB (Current){sizeCurrent}MB File Owner: (Previous){ownerPrevious} (Current){fileOwner}";
                                    _logHelper.WriteEventLog(eventLogMessage, EventLogEntryType.Warning, 7777);
                                }
                                dataReader.Close();
                                command.Dispose();
                            }
                        }
                        else
                        {
                            Log.Warning("File: '{path}' is newly created. Owner: {fileOwner} Hash: {tempHash}", path, fileOwner, tempHash);
                            string eventLogMessage = $"File: '{path}' is newly created. Owner: {fileOwner} Hash: {tempHash}";
                            _logHelper.WriteEventLog(eventLogMessage, EventLogEntryType.Warning, 7776);
                        }
                    }
                    //if there is no content in BASELINE_PATH, write to BASELINE_PATH instead
                    else
                    {
                        string sql = "INSERT INTO BASELINE_PATH (pathname, pathexists, filesize, owner, checktime, filehash, pathtype) " +
                                     $"VALUES ('{path}',true,{GetFileSize(path)},'{fileOwner}','(UTC){DateTime.UtcNow:yyyy/MM/dd hh:mm:ss tt}','{tempHash}','File')";
                        try
                        {
                            SQLiteHelper1.ExecuteNonQuery(sql);
                        }
                        catch (Exception e)
                        {
                            string errorMessage = "SQLite Exception: " + e.Message;
                            Log.Error(errorMessage);
                        }
                    }
                }
            }
        }

        private void CheckIfDeleted(bool haveBaseLinePath)
        {
            if (!haveBaseLinePath)
            {
                return;
            }

            string sql = "SELECT BASELINE_PATH.pathname, BASELINE_PATH.pathtype FROM BASELINE_PATH LEFT JOIN CURRENT_PATH ON BASELINE_PATH.pathname = CURRENT_PATH.pathname WHERE CURRENT_PATH.pathname IS NULL";
            SQLiteCommand command = new(sql, SQLiteHelper1.Connection);
            SQLiteDataReader dataReader = command.ExecuteReader();
            while (dataReader.Read())
            {
                string deletedPathName = dataReader.GetValue(0).ToString() ?? throw new InvalidOperationException();
                string deletedPathType = dataReader.GetValue(1).ToString() ?? throw new InvalidOperationException();
                string operation = "deleted";
                Log.Warning("{deletedPathType}: '{deletedPathName}' has been {operation}", deletedPathType, operation);
                string eventLogMessage = $"{deletedPathType}: '{deletedPathName}' has been {operation}";
                _logHelper.WriteEventLog(eventLogMessage, EventLogEntryType.Warning, 7778);
            }
            dataReader.Close();
        }

        private void ResetDatabaseTables(bool haveBaseLinePath)
        {
            if (!haveBaseLinePath)
            {
                return;
            }
            //delete all rows in BASELINE_PATH, copy all rows from CURRENT_PATH to BASELINE_PATH, then clear CURRENT_PATH
            string sql = "DELETE FROM BASELINE_PATH";
            SQLiteCommand command = new(sql, SQLiteHelper1.Connection);
            try
            {
                command.ExecuteNonQuery();
                command.Dispose();
            }
            catch (Exception e)
            {
                string errorMessage = "SQLite Exception: " + e.Message;
                Log.Error(errorMessage);
            }

            sql = "INSERT INTO BASELINE_PATH SELECT * FROM CURRENT_PATH";
            command = new(sql, SQLiteHelper1.Connection);
            try
            {
                command.ExecuteNonQuery();
                command.Dispose();
            }
            catch (Exception e)
            {
                string errorMessage = "SQLite Exception: " + e.Message;
                Log.Error(errorMessage);
            }

            sql = "DELETE FROM CURRENT_PATH";
            command = new(sql, SQLiteHelper1.Connection);
            try
            {
                command.ExecuteNonQuery();
                command.Dispose();
            }
            catch (Exception e)
            {
                string errorMessage = "SQLite Exception: " + e.Message;
                Log.Error(errorMessage);
            }
        }

        internal void FileIntegrityCheck()
        {
            SQLiteHelper1 = new();
            SQLiteHelper1.Open();
            int frequencyInMinutes = _logHelper.GetSchedule();
            Log.Information("Starting FIM checks on a {frequencyInMinutes} minute timer", frequencyInMinutes);
            if (_configurationOptions.IsCaptureRemoteConnectionStatus)
                Log.Information(_logHelper.GetRemoteConnections());
            bool haveBaseLinePath = CheckBaseLine(); //check if there is already data in the BASELINE_PATH table from a previous FIM check

            Stopwatch watch = new();
            watch.Start();

            try
            {
                string[] monListFileLines = GetFileMonList(); //get the list of paths in the monlist.txt file

                string[] pathList =
                    GetPathList(monListFileLines, haveBaseLinePath); //get the list of files / directories to watch

                string[] excludePathLines = GetFileExcludePath(); //get the list of paths in the exclude_path.txt file

                string[] excludePathList = GetExcludeList(excludePathLines);

                IEnumerable<string> finalPathList = pathList.Except(excludePathList); //filter exclusion file list

                foreach (string path in finalPathList)
                {
                    CheckPath(haveBaseLinePath, path);
                }

                CheckIfDeleted(haveBaseLinePath);
                ResetDatabaseTables(haveBaseLinePath);

                watch.Stop();
                string stopMessage = "Total time consumed in this round file integrity checking  = " +
                                     watch.ElapsedMilliseconds + "ms (" +
                                     Math.Round(Convert.ToDouble(watch.ElapsedMilliseconds) / 1000, 3)
                                         .ToString(CultureInfo.InvariantCulture) + "s).\n" +
                                     _logHelper.GetRemoteConnections();
                Log.Debug(stopMessage);
                _logHelper.WriteEventLog(stopMessage, EventLogEntryType.Information,7772);
                Log.Verbose("Total time consumed in this round file integrity checking  = " +
                            watch.ElapsedMilliseconds + "ms (" +
                            Math.Round(Convert.ToDouble(watch.ElapsedMilliseconds) / 1000, 3)
                                .ToString(CultureInfo.InvariantCulture) + "s).");
            }
            catch (Exception e)
            {
                string errorMessage = "Exception : " + e.Message +
                                      "\nPlease make sure all input entries are correct under \"monlist.txt\", \"exclude_path.txt\" and \"exclude_extension.txt\".\nPlease restart the service after correction.";
                Log.Error(errorMessage);
                _logHelper.WriteEventLog(errorMessage, EventLogEntryType.Error, 7773);
            }
            finally
            {
                SQLiteHelper1.Dispose();
            }
        }
    }
}

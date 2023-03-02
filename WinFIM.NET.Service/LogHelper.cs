using Serilog;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WinFIM.NET_Service
{
    internal static class LogHelper
    {
        internal static readonly string? WorkDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int Wow64DisableWow64FsRedirection(ref IntPtr ptr);

        private static readonly EventLog EventLog1 = new();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int Wow64EnableWow64FsRedirection(ref IntPtr ptr);

        //private static void AddOrUpdateAppSettings(string key, string value)
        //{
        //    try
        //    {
        //        var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        //        var settings = configFile.AppSettings.Settings;
        //        if (settings[key] == null)
        //        {
        //            settings.Add(key, value);
        //        }
        //        else
        //        {
        //            settings[key].Value = value;
        //        }
        //        configFile.Save(ConfigurationSaveMode.Modified);
        //        ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
        //    }
        //    catch (ConfigurationErrorsException)
        //    {
        //        Log.Error("Error writing app settings");
        //    }
        //}

        //internal static void ConfigureLogging()
        //{
        //    string logFilePath = ConfigurationManager.AppSettings["serilog:write-to:File.path"];
        //    if (!(string.IsNullOrEmpty(logFilePath)))
        //    {

        //        //if the configured log file path has a filename but not directory, set the directory to the same directory as this WinFIM.NET binary file
        //        string[] directoryDelimeters = { "/", "\\" };
        //        if (!(directoryDelimeters.Any(logFilePath.Contains)))
        //        {
        //            logFilePath = Path.Combine(LogHelper.WorkDir, logFilePath);
        //            AddOrUpdateAppSettings("serilog:write-to:File.path", logFilePath);
        //        }
        //    }
        //    var configuration = new ConfigurationBuilder()
        //        .AddJsonFile("appsettings.json")
        //        .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
        //        .Build();
        //    Log.Logger = new LoggerConfiguration()
        //        .ReadFrom.AppSettings()
        //        .CreateLogger();

        //    if (!(string.IsNullOrEmpty(logFilePath)))
        //    {
        //        Log.Information($"Logging to file: {logFilePath}");
        //    }

        //}

        internal static void Initialize()
        {
            if (!EventLog.SourceExists("WinFIM.NET"))
            {
                EventLog.CreateEventSource("WinFIM.NET", "WinFIM.NET");
            }
        }


        internal static void WriteEventLog(string message, EventLogEntryType eventType, int eventId)
        {
            if (Properties.Settings.Default.is_log_to_windows_eventlog)
            {
                EventLog1.Source = "WinFIM.NET";
                EventLog1.Log = "WinFIM.NET";
                message = message.Truncate(32768); // Windows Event log strings are limited to a maximum of 32768 characters
                EventLog1.WriteEntry(message, eventType, eventId);
            }
        }

        internal static string GetRemoteConnections()
        {
            string output = "ERROR in running CMD \"query user\"";
            if (!Properties.Settings.Default.is_capture_remote_connection_status)
                return string.Empty;
            try
            {
                using (Process process = new Process())
                {
                    IntPtr val = IntPtr.Zero;
                    _ = Wow64DisableWow64FsRedirection(ref val);
                    process.StartInfo.FileName = @"cmd.exe";
                    process.StartInfo.Arguments =
                        "/c \"@echo off & @for /f \"tokens=1,2,3,4,5\" %A in ('netstat -ano ^| findstr ESTABLISHED ^| findstr /v 127.0.0.1') do (@for /f \"tokens=1,2,5\" %F in ('qprocess \"%E\"') do (@IF NOT %H==IMAGE @echo %A , %B , %C , %D , %E , %F , %G , %H))\"";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();
                    // Synchronously read the standard output of the spawned process. 
                    output = "Established Remote Connection (snapshot)" + "\n";
                    output = output + "========================================" + "\n" +
                             "Proto | Local Address | Foreign Address | State | PID | USERNAME | SESSION NAME | IMAGE\n";
                    output += process.StandardOutput.ReadToEnd() + "\n";
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

        internal static int GetSchedule()
        {
            string schedulerConf = LogHelper.WorkDir + "\\scheduler.txt";
            int schedulerMin = 0;
            try
            {
                string timerMinute = File.ReadLines(schedulerConf).First();
                timerMinute = timerMinute.Trim();
                schedulerMin = Convert.ToInt32(timerMinute);
            }
            catch (IOException e)
            {
                string message = $"Please check if the file '{WorkDir}\\scheduler.txt' exists or a numeric value is input into the file 'scheduler.txt'. Defaulting to a timer of 0 minutes.";
                Log.Error(e, message);
            }
            return schedulerMin;
        }
    }
}

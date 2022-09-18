using Serilog;
using System;
using System.Diagnostics;
using System.ServiceProcess;

namespace WinFIM.NET_Service
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        private static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.AppSettings()
                .CreateLogger();

            string currentProcessName = Process.GetCurrentProcess().ProcessName;
            if (Process.GetProcessesByName(currentProcessName).Length > 1)
            {
                Log.Error($"Application {currentProcessName} already running. Only one instance of this application is allowed. Exiting");
                return;
            }


            if (Environment.UserInteractive)
            {
                // Startup as application
                using (Service1 service1 = new Service1())
                {
                    Log.Debug(("Starting WinFIM.NET in console mode"));
                    service1.ConsoleScheduled();
                    Log.Debug(("Exiting WinFIM.NET console mode"));
                }
            }
            else
            {
                // Startup as service
                ServiceBase[] servicesToRun = new ServiceBase[]
                {
                    new Service1()
                };
                ServiceBase.Run(servicesToRun);
            }
        }
    }
}

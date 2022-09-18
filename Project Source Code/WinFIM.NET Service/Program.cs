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
                    Log.Debug(("Running in console app mode"));
                    service1.TestStartupAndStop();
                    Log.Debug(("Exiting"));
                }

                //Controller controller = new Controller();
                //Log.Debug(("Running in console app mode"));
                //controller.Initialise();
                //controller.FileIntegrityCheck();
                //Log.Debug(("Exiting"));
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

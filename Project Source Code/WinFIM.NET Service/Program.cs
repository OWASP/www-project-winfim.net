using Serilog;
using System;
using System.ServiceProcess;

namespace WinFIM.NET_Service
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.AppSettings()
                .CreateLogger();

            if (Environment.UserInteractive)
            {
                // Startup as application
                using (Service1 service1 = new Service1())
                {
                    Log.Debug(("Running in console app mode"));
                    service1.TestStartupAndStop(null);
                    Log.Debug(("Exiting"));
                }
            }
            else
            {
                // Startup as service
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new Service1()
                };
                ServiceBase.Run(ServicesToRun);

            }
        }
    }
}

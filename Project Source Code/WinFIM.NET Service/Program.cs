using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace WinFIM.NET_Service
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            if (Environment.UserInteractive)
            {
                // Startup as application
                Service1 service1 = new Service1();
                service1.TestStartupAndStop(null);
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

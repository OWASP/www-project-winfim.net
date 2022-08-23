using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Threading.Tasks;
using System.ServiceProcess;

namespace WinFIM.NET_Service
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }


        //for service to start once after install
        private void ServiceInstaller_AfterInstall(object sender, InstallEventArgs e)
        {
            ServiceInstaller serviceInstaller = (ServiceInstaller)sender;

            try
            {
                using (ServiceController sc = new ServiceController(serviceInstaller.ServiceName))
                {
                    sc.Start();
                }
            }
            catch (Exception e1)
            {
                Console.WriteLine(e1.Message);
            }

        }

        private void serviceProcessInstaller1_AfterInstall(object sender, InstallEventArgs e)
        {

        }
        
    }

}

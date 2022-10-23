using Serilog;
using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Timers;
// ReSharper disable FunctionNeverReturns

namespace WinFIM.NET_Service
{
    public partial class Service1 : ServiceBase
    {
        private readonly Controller _controller = new Controller();

        public Service1()
        {
            InitializeComponent();
            LogHelper.Initialize();
        }

        // For testing the Service Start method when started from the console (easier than attaching a debugger to a running service)
        internal void ConsoleTest()
        {
            ServiceStart();
            //this.OnStop();
        }

        // run in scheduled / continuous mode if the "WinFIM.NET Service.exe" executable is directly run as a console app (as opposed to running as a service)
        internal void ConsoleScheduled()
        {
            _controller.Initialise();
            int schedulerMin = LogHelper.GetSchedule();
            string serviceStartMessage = Properties.Settings.Default.service_start_message +
                                         $": (UTC) {DateTime.UtcNow:yyyy/dd/MM hh:mm:ss tt}\n\n";
            serviceStartMessage = serviceStartMessage + LogHelper.GetRemoteConnections() +
                                  "This console started service will run every " + schedulerMin.ToString() +
                                  " minute(s).";
            Log.Information(serviceStartMessage);
            LogHelper.WriteEventLog(serviceStartMessage, EventLogEntryType.Information, 7771);

            _controller.FileIntegrityCheck();

            while (true)
            {
                Thread.Sleep(1000 * 60 * schedulerMin); // sleep for the number of minutes specified in scheduler.txt
                _controller.FileIntegrityCheck();
            }
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
            // Read if there is any valid schedule timer (in minute)
            _controller.Initialise();
            string serviceStartMessage;
            int schedulerMin = LogHelper.GetSchedule();

            if (schedulerMin > 0)
            // using timer mode
            {
                System.Timers.Timer timer = new System.Timers.Timer
                {
                    Interval = schedulerMin * 60000 // control the service to run every pre-defined minutes
                };
                timer.Elapsed += OnTimer;
                timer.Start();
                serviceStartMessage = Properties.Settings.Default.service_start_message +
                                      $": (UTC) {DateTime.UtcNow:yyyy/dd/MM hh:mm:ss tt}\n\n";
                serviceStartMessage = serviceStartMessage + LogHelper.GetRemoteConnections() +
                                      $"This service will run every {schedulerMin.ToString()} minute(s).";
                if (Properties.Settings.Default.is_capture_remote_connection_status)
                {
                    Log.Information(serviceStartMessage);
                    LogHelper.WriteEventLog(serviceStartMessage, EventLogEntryType.Information, 7771);
                }

                _controller.FileIntegrityCheck();
            }
            else
            // run in continuous mode
            {
                serviceStartMessage = Properties.Settings.Default.service_start_message +
                                      ": (UTC) {DateTime.UtcNow:yyyy/dd/MM hh:mm:ss tt}\n\n";
                serviceStartMessage = serviceStartMessage + LogHelper.GetRemoteConnections() +
                                      "This service will run continuously.";
                Log.Information(serviceStartMessage);
                LogHelper.WriteEventLog(serviceStartMessage, EventLogEntryType.Information, 7771);
                while (true)
                {
                    Log.Debug("Looping in continuous mode");
                    _controller.FileIntegrityCheck();
                }
            }
        }

        private void OnTimer(object sender, ElapsedEventArgs args)
        {
            Log.Debug("Looping on timer");
            _controller.FileIntegrityCheck();
        }

        protected override void OnStop()
        {
            string serviceStopMessage = Properties.Settings.Default.service_stop_message +
                                        $": (UTC) {DateTime.UtcNow:yyyy/dd/MM hh:mm:ss tt}\n\n";
            serviceStopMessage += LogHelper.GetRemoteConnections();
            Log.Information(serviceStopMessage);
            LogHelper.WriteEventLog(serviceStopMessage, EventLogEntryType.Information, 7770);
        }
    }
}
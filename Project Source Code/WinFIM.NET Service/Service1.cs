using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Timers;

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

        // run in scheduled / continous mode if the "WinFIM.NET Service.exe" executable is directly run as a console app (as opposed to running as a service)
        internal void ConsoleScheduled()
        {
            _controller.Initialise();
            string schedulerConf = _controller.WorkDir + "\\scheduler.txt";
            int schedulerMin;
            try
            {
                string timerMinute = File.ReadLines(schedulerConf).First();
                timerMinute = timerMinute.Trim();
                schedulerMin = Convert.ToInt32(timerMinute);


                string serviceStartMessage = Properties.Settings.Default.service_start_message + ": (UTC) " + DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt") + "\n\n";
                serviceStartMessage = serviceStartMessage + LogHelper.GetRemoteConnections() + "\nThis console started service will run every " + schedulerMin.ToString() + " minute(s).";
                Log.Debug(serviceStartMessage);
                LogHelper.WriteEventLog(serviceStartMessage, EventLogEntryType.Information, 7771); // setting the Event ID as 7771
                _controller.FileIntegrityCheck();

                while (true)
                {
                    Thread.Sleep(1000 * 60 * schedulerMin); // sleep for the number of minutes specified in scheduler.txt
                    _controller.FileIntegrityCheck();
                }
            }
            catch (IOException e)
            {
                string serviceStartMessage = "Please check if the file 'scheduler.txt' exists or a numeric value is input into the file 'scheduler.txt'.\nIt will run in continuous mode.";
                Log.Error($"Exception: {e.Message} - {serviceStartMessage}");
                LogHelper.WriteEventLog($"Exception: {e.Message}\n{serviceStartMessage}", EventLogEntryType.Error, 7773); //setting the Event ID as 7773
                schedulerMin = 0;
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
            string schedulerConf = _controller.WorkDir + "\\scheduler.txt";
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
                LogHelper.WriteEventLog($"Exception: {e.Message}\n{serviceStartMessage}", EventLogEntryType.Error, 7773); //setting the Event ID as 7773
                schedulerMin = 0;
            }

            if (schedulerMin > 0)
            // using timer mode
            {
                System.Timers.Timer timer = new System.Timers.Timer
                {
                    Interval = schedulerMin * 60000 // control the service to run every pre-defined minutes
                };
                timer.Elapsed += OnTimer;
                timer.Start();
                serviceStartMessage = Properties.Settings.Default.service_start_message + ": (UTC) " + DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt") + "\n\n";
                serviceStartMessage = serviceStartMessage + LogHelper.GetRemoteConnections() + "\nThis service will run every " + schedulerMin.ToString() + " minute(s).";
                Log.Debug(serviceStartMessage);
                LogHelper.WriteEventLog(serviceStartMessage, EventLogEntryType.Information, 7771); // setting the Event ID as 7771
                _controller.FileIntegrityCheck();
            }
            else
            // run in continuous mode
            {
                serviceStartMessage = Properties.Settings.Default.service_start_message + ": (UTC) " + DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt") + "\n\n";
                serviceStartMessage = serviceStartMessage + LogHelper.GetRemoteConnections() + "\nThis service will run continuously.";
                Log.Debug(serviceStartMessage);
                LogHelper.WriteEventLog(serviceStartMessage, EventLogEntryType.Information, 7771); // setting the Event ID as 7771
                bool trackerBoolean = true;
                while (trackerBoolean)
                {
                    trackerBoolean = _controller.FileIntegrityCheck();
                }
            }
        }

        private void OnTimer(object sender, ElapsedEventArgs args)
        {
            _controller.FileIntegrityCheck();
        }

        protected override void OnStop()
        {
            string serviceStopMessage = Properties.Settings.Default.service_stop_message + ": (UTC) " + DateTime.UtcNow.ToString(@"M/d/yyyy hh:mm:ss tt") + "\n\n";
            serviceStopMessage = serviceStopMessage + LogHelper.GetRemoteConnections() + "\n";
            Log.Information(serviceStopMessage);
            LogHelper.WriteEventLog(serviceStopMessage, EventLogEntryType.Information, 7770); //setting the Event ID as 7770
        }

    }
}

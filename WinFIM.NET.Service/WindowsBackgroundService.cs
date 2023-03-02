using Microsoft.Extensions.Hosting;
using Serilog;
using System.Diagnostics;

namespace WinFIM.NET_Service
{
    public sealed class WindowsBackgroundService : BackgroundService
    {
        private readonly Controller _controller;
        private readonly ILogger _logger;

        public WindowsBackgroundService(
            Controller controller,
            ILogger logger) => (_controller, _logger) = (controller, logger);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _controller.Initialise();
                LogHelper.Initialize();
                int schedulerMin = LogHelper.GetSchedule();
                string serviceStartMessage = Properties.Settings.Default.service_start_message +
                                             $": (UTC) {DateTime.UtcNow:yyyy/MM/dd hh:mm:ss tt}";
                serviceStartMessage = $"{serviceStartMessage + LogHelper.GetRemoteConnections()} " +
                                      "This console started service will run every " + schedulerMin.ToString() +
                                      " minute(s).";
                Log.Information(serviceStartMessage);
                LogHelper.WriteEventLog(serviceStartMessage, EventLogEntryType.Information, 7771);

                while (!stoppingToken.IsCancellationRequested)
                {
                    _controller.FileIntegrityCheck();

                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "{Message}", ex.Message);

                // Terminates this process and returns an exit code to the operating system.
                // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
                // performs one of two scenarios:
                // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
                // 2. When set to "StopHost": will cleanly stop the host, and log errors.
                //
                // In order for the Windows Service Management system to leverage configured
                // recovery options, we need to terminate the process with a non-zero exit code.
                Environment.Exit(1);
            }
        }
    }

}

using Microsoft.Extensions.Hosting;
using Serilog;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace WinFIM.NET_Service
{
    [SupportedOSPlatform("windows")]
    public sealed class WindowsBackgroundService : BackgroundService
    {
        private readonly Controller _controller;
        private readonly LogHelper _logHelper;

        public WindowsBackgroundService(Controller controller, LogHelper logHelper)
        {
            _controller = controller;
            _logHelper = logHelper;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _controller.Initialise();
                LogHelper.Initialize();
                int frequencyInMinutes = _logHelper.GetSchedule();
                string remoteConnections = _logHelper.GetRemoteConnections();
                Log.Information("WinFIM.NET Started. Frequency: {frequencyInMinutes} minutes. Remote connections: {RemoteConnections}", frequencyInMinutes, remoteConnections);
                string eventLogMessage = $"WinFIM.NET Started. Frequency: {frequencyInMinutes} minutes\n {_logHelper.GetRemoteConnections()}";
                _logHelper.WriteEventLog(eventLogMessage, EventLogEntryType.Information, 7771);

                while (!stoppingToken.IsCancellationRequested)
                {
                    _controller.FileIntegrityCheck();

                    await Task.Delay(TimeSpan.FromMinutes(frequencyInMinutes), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Message}", ex.Message);

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

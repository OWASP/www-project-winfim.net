using Microsoft.Extensions.Configuration;

namespace WinFIM.NET.Service
{
    public class ConfigurationOptions
    {
        public bool IsLogToWindowsEventLog { get; set; }
        public bool IsCaptureRemoteConnectionStatus { get; set; }

        public string Timer { get; set; } = "0";

        public ConfigurationOptions(IConfiguration configuration)
        {
            configuration.Bind("ConfigurationOptions", this);
        }

    }

}

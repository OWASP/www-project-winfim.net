using Microsoft.Extensions.Configuration;

namespace WinFIM.NET_Service
{
    public class ConfigurationOptions
    {
        public bool IsLogToWindowsEventLog { get; set; }
        public bool IsCaptureRemoteConnectionStatus { get; set; }

        public ConfigurationOptions(IConfiguration configuration)
        {
            configuration.Bind("ConfigurationOptions", this);
        }

    }

}

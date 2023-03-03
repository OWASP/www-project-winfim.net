using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Runtime.Versioning;
using WinFIM.NET_Service;

namespace WinFIM.NET_Service
{
    [SupportedOSPlatform("windows")]
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var builder = new ConfigurationBuilder();
            BuildConfig(builder,args);
            var configuration = builder.Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            Log.Information("Application Starting");
            
            var configurationOptions = new ConfigurationOptions(configuration);
            IHost host = Host.CreateDefaultBuilder(args)
                .UseWindowsService(options => { options.ServiceName = "WinFIM.NET.Service"; })
                .ConfigureServices(services =>
                {
                    services.AddSingleton(configurationOptions);
                    services.AddSingleton<LogHelper>();
                    services.AddSingleton <Controller>();
                    services.AddHostedService<WindowsBackgroundService>();
                })
                .UseSerilog()
                .Build();

            await host.RunAsync();
        }

        private static void BuildConfig(IConfigurationBuilder builder, string[] args)
        {
            string? env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            builder.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args);
        }
    }
}
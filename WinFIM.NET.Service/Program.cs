using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using WinFIM.NET_Service;

string? env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "WinFIM.NET.Service";
    })
    .ConfigureAppConfiguration(
        (hostContext, config) =>
        {
            config.SetBasePath(Directory.GetCurrentDirectory());
            config.AddJsonFile("appsettings.json", false, true);
            config.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true);
            config.AddCommandLine(args);
        }
    )
    .ConfigureServices(services =>
    {
        services.AddSingleton<Controller>();
        services.AddHostedService<WindowsBackgroundService>();
    })
    .UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration))
    .Build();
await host.RunAsync();

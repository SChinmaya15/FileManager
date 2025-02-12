using System.Diagnostics;
using FileSystemMonitor.Core.Interfaces;
using FileSystemMonitor.Core.Models;
using FileSystemMonitor.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {

            var host = CreateHostBuilder(args).Build();
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            // Log startup error
            EventLog.WriteEntry("FileSystemMonitor",
                $"Service failed to start: {ex.Message}",
                EventLogEntryType.Error);
            throw;
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
       return Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .ConfigureServices((hostContext, services) =>
            {
               services.Configure<MonitorSettings>(hostContext.Configuration.GetSection("MonitorSettings"));

                services.AddSingleton<IFileActivityLogger,FileActivityLogger>();

                services.AddSingleton<IKafkaProducer, KafkaProducerService>();
                services.AddHostedService<WindowsBackgroundService>();
            })
            .ConfigureLogging((hostContext, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole(); // Adds console logging
                logging.AddDebug(); // Adds debug logging
                logging.AddEventLog(); // Adds Event Log provider
                logging.AddProvider(new FileLoggerProvider("logs")); // Adds file logging
            })
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            });
    }
}
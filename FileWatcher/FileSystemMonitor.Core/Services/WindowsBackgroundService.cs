using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSystemMonitor.Core.Interfaces;
using FileSystemMonitor.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileSystemMonitor.Core.Services
{
    public class WindowsBackgroundService : BackgroundService
    {
        private readonly ILogger<WindowsBackgroundService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHostApplicationLifetime _hostLifetime;
        private FileSystemMonitorService _monitorService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptionsMonitor<MonitorSettings> _settings;

        public WindowsBackgroundService(
            ILogger<WindowsBackgroundService> logger,
            IConfiguration configuration,
            IHostApplicationLifetime hostLifetime,
            IServiceProvider serviceProvider,
            IOptionsMonitor<MonitorSettings> settings)
        {
            _logger = logger;
            _configuration = configuration;
            _hostLifetime = hostLifetime;
            _serviceProvider = serviceProvider;
            _settings = settings;

            // Monitor configuration changes
            _settings.OnChange(async (settings) =>
            {
                await RestartMonitoringAsync(settings);
            });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var settings = _settings.CurrentValue;

                    if (!settings.Enabled)
                    {
                        _logger.LogInformation("File system monitoring is disabled in configuration");
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                        continue;
                    }

                    try
                    {
                        await StartMonitoringAsync(settings, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in file system monitoring");

                        if (settings.AutoRestartOnError)
                        {
                            _logger.LogInformation("Attempting to restart monitoring in 30 seconds...");
                            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Service is shutting down");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Fatal error in monitoring service");
                _hostLifetime.StopApplication();
            }
        }

        private async Task StartMonitoringAsync(MonitorSettings settings, CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<IFileActivityLogger>();
                var kafkaProducer = scope.ServiceProvider.GetRequiredService<IKafkaProducer>();

                _monitorService = new FileSystemMonitorService(
                    settings.MonitorPath,
                    logger,
                    kafkaProducer,
                    settings.BatchSize,
                    settings.BatchTimeoutMs);

                _logger.LogInformation("File system monitoring started for path: {Path}", settings.MonitorPath);

                // Keep the service running until cancellation is requested
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
        }

        private async Task RestartMonitoringAsync(MonitorSettings settings)
        {
            _logger.LogInformation("Configuration changed, restarting monitoring service...");

            if (_monitorService != null)
            {
                _monitorService.Dispose();
                _monitorService = null;
            }

            if (settings.Enabled)
            {
                await StartMonitoringAsync(settings, CancellationToken.None);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping file system monitoring service...");

            if (_monitorService != null)
            {
                _monitorService.Dispose();
                _monitorService = null;
            }

            await base.StopAsync(cancellationToken);
        }
    }
}

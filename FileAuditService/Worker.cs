using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using FileAuditService.Core.Interfaces;

namespace FileAuditService
{
    public class Worker : BackgroundService
    {
        #region Fields
        private readonly ILogger<Worker> _logger;
        private readonly IAuditor _auditor;
        #endregion

        public Worker(ILogger<Worker> logger, IAuditor auditor)
        {
            _logger = logger;
            _auditor = auditor;
            _logger.LogInformation("Audit Service loaded.");
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Worker started at: {DateTime.Now}");
            bool started = _auditor.Start();
            if (!started)
                await StopAsync(cancellationToken);
            else 
                await base.StartAsync(cancellationToken);
        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Worker stopped at: {DateTime.Now}");
            _auditor.Stop();
            return base.StopAsync(cancellationToken);
        }
        public override void Dispose()
        {
            _logger.LogInformation($"Worker disposed at: {DateTime.Now}");
            base.Dispose();
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }
    }
}

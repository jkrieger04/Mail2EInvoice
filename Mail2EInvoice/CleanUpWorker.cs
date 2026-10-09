namespace Mail2EInvoice
{
    public class CleanUpWorker : BackgroundService
    {
        private readonly ILogger<CleanUpWorker> _logger;
        private readonly EMLWorkerConfiguration _configuration;

        public CleanUpWorker(ILogger<CleanUpWorker> logger)
        {
            _logger = logger;
            _configuration = EMLWorkerConfigurationHelper.Load();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CleanUpWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                DateTime now = DateTime.Now;

                DateTime nextRun = new DateTime(
                    now.Year,
                    now.Month,
                    now.Day,
                    3, 0, 0
                );

                if (nextRun <= now)
                    nextRun = nextRun.AddDays(1);

                TimeSpan delay = nextRun - now;

                _logger.LogInformation($"Clean Up Next run at {nextRun}");

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                try
                {
                    _logger.LogInformation($"Clean Up Task executed at {DateTime.Now}");

                    var cutoffDateBackup = DateTime.Now.AddDays(-10);
                    var cutoffDateError = DateTime.Now.AddDays(-30);
                    foreach (var configurationFolder in _configuration.ConfigurationFolders)
                    {
                        foreach (var file in Directory.GetFiles(configurationFolder.BackupDirectory))
                        {
                            if (File.GetLastWriteTime(file) < cutoffDateBackup)
                            {
                                File.Delete(file);
                            }
                        }

                        foreach (var file in Directory.GetFiles(configurationFolder.ErrorDirectory))
                        {
                            if (File.GetLastWriteTime(file) < cutoffDateError)
                            {
                                File.Delete(file);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing clean up task");
                }
            }

            _logger.LogInformation("CleanUpWorker stopping.");
        }
    }
}

namespace ServiceAppDMSRecieving
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ImmsRCRcsvFile _dmscsvFileService;
        private readonly ImmsPORAcsvFile _mmsPORAcsvFileService;

        public Worker(ILogger<Worker> logger, ImmsRCRcsvFile dmscsvFileService, ImmsPORAcsvFile mmsPORAcsvFileService)
        {
            _logger = logger;
            _dmscsvFileService = dmscsvFileService;
            _mmsPORAcsvFileService = mmsPORAcsvFileService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _dmscsvFileService.CopyCSVfileMMS_RCR();
                _mmsPORAcsvFileService.CopyCSVfileMMS_PORA();

                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(3000, stoppingToken);
            }
        }
    }
}

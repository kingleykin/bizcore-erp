using Payment.API.Application.Services;

namespace Payment.API.Application.BackgroundServices
{
    /// <summary>
    /// Background service để cleanup expired idempotency records.
    /// Chạy mỗi 1 giờ, xóa records đã hết hạn để tránh table phình to.
    /// </summary>
    public class IdempotencyCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<IdempotencyCleanupService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(1);

        public IdempotencyCleanupService(
            IServiceProvider serviceProvider,
            ILogger<IdempotencyCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("IdempotencyCleanupService started");

            // Đợi 1 phút trước khi chạy lần đầu (để service khởi động xong)
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredRecordsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during idempotency cleanup");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("IdempotencyCleanupService stopped");
        }

        private async Task CleanupExpiredRecordsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var idempotencyService = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();

            var deletedCount = await idempotencyService.CleanupExpiredRecordsAsync(cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Idempotency cleanup completed: {Count} records deleted",
                    deletedCount);
            }
            else
            {
                _logger.LogDebug("Idempotency cleanup: no expired records found");
            }
        }
    }
}

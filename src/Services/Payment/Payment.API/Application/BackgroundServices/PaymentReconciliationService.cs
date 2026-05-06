using Microsoft.EntityFrameworkCore;
using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data;

namespace Payment.API.Application.BackgroundServices
{
    /// <summary>
    /// Background service để reconcile payments bị stuck ở Processing.
    /// Chạy mỗi 5 phút, tìm payments Processing > 5 phút → mark Failed.
    /// 
    /// Đây là safety net cuối cùng khi:
    /// - Saga timeout không fire
    /// - Event bị mất
    /// - Consumer crash
    /// </summary>
    public class PaymentReconciliationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PaymentReconciliationService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _stuckThreshold = TimeSpan.FromMinutes(5);

        public PaymentReconciliationService(
            IServiceProvider serviceProvider,
            ILogger<PaymentReconciliationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PaymentReconciliationService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ReconcileStuckPaymentsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during payment reconciliation");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("PaymentReconciliationService stopped");
        }

        private async Task ReconcileStuckPaymentsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cutoff = DateTime.UtcNow - _stuckThreshold;

            var stuckPayments = await context.Payments
                .Where(p => p.Status == PaymentStatus.Processing && p.PaymentDate < cutoff)
                .ToListAsync(cancellationToken);

            if (stuckPayments.Count == 0)
            {
                _logger.LogDebug("No stuck payments found");
                return;
            }

            _logger.LogWarning(
                "Found {Count} stuck payments (Processing > {Minutes} minutes)",
                stuckPayments.Count, _stuckThreshold.TotalMinutes);

            foreach (var payment in stuckPayments)
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = $"Payment stuck in Processing state. Auto-failed by reconciliation job after {_stuckThreshold.TotalMinutes} minutes.";

                _logger.LogWarning(
                    "Auto-failed stuck payment PaymentId={PaymentId} InvoiceId={InvoiceId} Age={Age}",
                    payment.Id, payment.InvoiceId, DateTime.UtcNow - payment.PaymentDate);
            }

            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Reconciliation completed: {Count} payments auto-failed",
                stuckPayments.Count);
        }
    }
}

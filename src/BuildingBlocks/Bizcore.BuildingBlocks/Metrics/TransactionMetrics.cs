using Prometheus;

namespace Bizcore.BuildingBlocks.Metrics;

/// <summary>
/// Prometheus metrics for transaction monitoring and observability.
/// </summary>
public static class TransactionMetrics
{
    /// <summary>
    /// Histogram tracking transaction duration in seconds.
    /// Labels: service, operation, status
    /// </summary>
    public static readonly Histogram TransactionDuration = Prometheus.Metrics.CreateHistogram(
        "transaction_duration_seconds",
        "Duration of database transactions",
        new HistogramConfiguration
        {
            LabelNames = new[] { "service", "operation", "status" },
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
        }
    );

    /// <summary>
    /// Counter tracking total number of transactions.
    /// Labels: service, operation, status
    /// </summary>
    public static readonly Counter TransactionTotal = Prometheus.Metrics.CreateCounter(
        "transaction_total",
        "Total number of transactions",
        new CounterConfiguration
        {
            LabelNames = new[] { "service", "operation", "status" }
        }
    );

    /// <summary>
    /// Gauge tracking number of pending messages in outbox.
    /// Labels: service
    /// </summary>
    public static readonly Gauge OutboxPendingCount = Prometheus.Metrics.CreateGauge(
        "outbox_pending_count",
        "Number of pending messages in outbox",
        new GaugeConfiguration
        {
            LabelNames = new[] { "service" }
        }
    );

    /// <summary>
    /// Counter tracking total number of messages delivered from outbox.
    /// Labels: service, status
    /// </summary>
    public static readonly Counter OutboxDeliveredTotal = Prometheus.Metrics.CreateCounter(
        "outbox_delivered_total",
        "Total number of messages delivered from outbox",
        new CounterConfiguration
        {
            LabelNames = new[] { "service", "status" }
        }
    );

    /// <summary>
    /// Counter tracking number of duplicate messages detected by inbox.
    /// Labels: service, consumer
    /// </summary>
    public static readonly Counter InboxDuplicateCount = Prometheus.Metrics.CreateCounter(
        "inbox_duplicate_count",
        "Number of duplicate messages detected",
        new CounterConfiguration
        {
            LabelNames = new[] { "service", "consumer" }
        }
    );

    /// <summary>
    /// Gauge tracking number of active sagas.
    /// Labels: saga_type, state
    /// </summary>
    public static readonly Gauge SagaActiveCount = Prometheus.Metrics.CreateGauge(
        "saga_active_count",
        "Number of active sagas",
        new GaugeConfiguration
        {
            LabelNames = new[] { "saga_type", "state" }
        }
    );

    /// <summary>
    /// Counter tracking number of saga timeouts.
    /// Labels: saga_type
    /// </summary>
    public static readonly Counter SagaTimeoutCount = Prometheus.Metrics.CreateCounter(
        "saga_timeout_count",
        "Number of saga timeouts",
        new CounterConfiguration
        {
            LabelNames = new[] { "saga_type" }
        }
    );

    /// <summary>
    /// Counter tracking number of compensations triggered.
    /// Labels: service, reason
    /// </summary>
    public static readonly Counter CompensationCount = Prometheus.Metrics.CreateCounter(
        "compensation_count",
        "Number of compensations triggered",
        new CounterConfiguration
        {
            LabelNames = new[] { "service", "reason" }
        }
    );

    /// <summary>
    /// Counter tracking number of messages sent to dead letter queue.
    /// Labels: service, consumer, reason
    /// </summary>
    public static readonly Counter DlqMessageCount = Prometheus.Metrics.CreateCounter(
        "dlq_message_count",
        "Number of messages sent to dead letter queue",
        new CounterConfiguration
        {
            LabelNames = new[] { "service", "consumer", "reason" }
        }
    );
}

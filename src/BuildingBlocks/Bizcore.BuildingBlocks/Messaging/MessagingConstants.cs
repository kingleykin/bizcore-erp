namespace Bizcore.BuildingBlocks.Messaging;

public static class MessagingConstants
{
    /// <summary>
    /// TTL for retry queues (30 seconds).
    /// Business queues should NOT have TTL.
    /// </summary>
    public const int RetryTtlMs = 30000;

    /// <summary>
    /// Shared Dead Letter Exchange name for the entire system.
    /// </summary>
    public const string SharedDeadLetterExchange = "bizcore.dlx";
}

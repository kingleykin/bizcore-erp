using Bizcore.BuildingBlocks.Abstractions;

namespace Customer.API.Domain.Entities
{
    /// <summary>
    /// Lịch sử cộng điểm thưởng — append-only. Unique theo OrderId (xem CustomerPointsTransactionConfiguration)
    /// để OrderConfirmedConsumer idempotent khi OrderConfirmedEvent bị MassTransit redeliver: kiểm
    /// tra bản ghi đã tồn tại cho OrderId trước khi cộng điểm lần nữa.
    /// </summary>
    public class CustomerPointsTransaction : BaseEntity
    {
        public Guid CustomerId         { get; private set; }
        public Guid OrderId            { get; private set; }
        public int  PointsAwarded      { get; private set; }
        public int  PointsBalanceAfter { get; private set; }

        private CustomerPointsTransaction() { }

        public static CustomerPointsTransaction Create(Guid customerId, Guid orderId, int pointsAwarded, int pointsBalanceAfter)
        {
            return new CustomerPointsTransaction
            {
                CustomerId = customerId,
                OrderId = orderId,
                PointsAwarded = pointsAwarded,
                PointsBalanceAfter = pointsBalanceAfter
            };
        }
    }
}

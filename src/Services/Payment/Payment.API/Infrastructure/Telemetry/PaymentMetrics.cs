using System.Diagnostics.Metrics;

namespace Payment.API.Infrastructure.Telemetry
{
    public class PaymentMetrics
    {
        public const string MeterName = "Bizcore.Payment";
        private readonly Counter<int> _paymentCompleted;
        private readonly Counter<int> _paymentReversed;
        private readonly Histogram<double> _paymentAmount;

        public PaymentMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create(MeterName);
            _paymentCompleted = meter.CreateCounter<int>("payment_completed_total", "Completed");
            _paymentReversed = meter.CreateCounter<int>("payment_reversed_total", "Reversed");
            _paymentAmount = meter.CreateHistogram<double>("payment_amount", "Amount", "USD");
        }

        public void PaymentCompleted(decimal amount)
        {
            _paymentCompleted.Add(1);
            _paymentAmount.Record((double)amount);
        }

        public void PaymentReversed()
        {
            _paymentReversed.Add(1);
        }
    }
}

namespace Report.API.Domain.Entities
{
    public class Invoice
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public int Status { get; set; } // 0: Pending, 1: Paid
    }
}

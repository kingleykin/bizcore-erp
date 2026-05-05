namespace Report.API.DTOs
{
    public class DashboardStatsDto
    {
        public int TotalInvoices { get; set; }
        public decimal TotalRevenue { get; set; }
        public int PaidInvoices { get; set; }
        public int PendingInvoices { get; set; }
    }
}

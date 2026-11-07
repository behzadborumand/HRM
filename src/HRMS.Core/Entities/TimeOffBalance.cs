namespace HRMS.Core.Entities
{
    public class TimeOffBalance
    {
        public int BalanceId { get; set; }
        public int EmployeeId { get; set; }
        public int PolicyId { get; set; }
        public int Year { get; set; }
        public decimal AccruedHours { get; set; }
        public decimal UsedHours { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}

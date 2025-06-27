using System;

namespace QuickTechSystems.Application.DTOs
{
    public class EmployeeAttendanceDTO : BaseDTO
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal StandardDailyHours { get; set; } = 8.0m;
        public decimal OvertimeMultiplier { get; set; } = 1.5m;
        public decimal TotalHoursWorked { get; set; }
        public decimal RegularHours { get; set; }
        public decimal OvertimeHours { get; set; }
        public decimal RegularPay { get; set; }
        public decimal OvertimePay { get; set; }
        public decimal TotalDailyPay { get; set; }
    }
}
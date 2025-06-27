using System;

namespace QuickTechSystems.Domain.Entities
{
    public class EmployeeAttendance
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal StandardDailyHours { get; set; } = 8.0m;
        public decimal OvertimeMultiplier { get; set; } = 1.5m;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public virtual Employee Employee { get; set; } = null!;

        public decimal TotalHoursWorked
        {
            get
            {
                var totalMinutes = (EndTime - StartTime).TotalMinutes;
                return totalMinutes > 0 ? (decimal)(totalMinutes / 60.0) : 0m;
            }
        }

        public decimal RegularHours
        {
            get
            {
                var totalHours = TotalHoursWorked;
                return totalHours <= StandardDailyHours ? totalHours : StandardDailyHours;
            }
        }

        public decimal OvertimeHours
        {
            get
            {
                var totalHours = TotalHoursWorked;
                return totalHours > StandardDailyHours ? totalHours - StandardDailyHours : 0m;
            }
        }

        public decimal RegularPay
        {
            get => RegularHours * HourlyRate;
        }

        public decimal OvertimePay
        {
            get => OvertimeHours * HourlyRate * OvertimeMultiplier;
        }

        public decimal TotalDailyPay
        {
            get => RegularPay + OvertimePay;
        }
    }
}
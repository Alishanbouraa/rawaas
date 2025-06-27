using System;
using System.Globalization;

namespace QuickTechSystems.Application.DTOs
{
    public class AttendanceSummaryDTO
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int Month { get; set; }
        public int Year { get; set; }
        public int TotalDaysWorked { get; set; }
        public decimal TotalRegularHours { get; set; }
        public decimal TotalOvertimeHours { get; set; }
        public decimal TotalRegularPay { get; set; }
        public decimal TotalOvertimePay { get; set; }
        public decimal GrandTotalSalary { get; set; }
        public decimal AverageHoursPerDay { get; set; }
        public decimal AverageDailyPay { get; set; }

        public string MonthName
        {
            get
            {
                if (Month >= 1 && Month <= 12)
                {
                    var monthNames = new[]
                    {
                        "January", "February", "March", "April", "May", "June",
                        "July", "August", "September", "October", "November", "December"
                    };
                    return monthNames[Month - 1];
                }
                return "Unknown";
            }
        }

        public string MonthYearDisplay => $"{MonthName} {Year}";

        public decimal TotalHours => TotalRegularHours + TotalOvertimeHours;

        public decimal OvertimePercentage
        {
            get
            {
                if (TotalHours == 0) return 0;
                return (TotalOvertimeHours / TotalHours) * 100;
            }
        }

        public decimal PayPerHour
        {
            get
            {
                if (TotalHours == 0) return 0;
                return GrandTotalSalary / TotalHours;
            }
        }

        public bool HasData => TotalDaysWorked > 0;
    }
}
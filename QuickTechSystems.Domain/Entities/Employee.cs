using System;
using System.Collections.Generic;

namespace QuickTechSystems.Domain.Entities
{
    public class Employee
    {
        public int EmployeeId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime? LastLogin { get; set; }
        public decimal MonthlySalary { get; set; }
        public decimal CurrentBalance { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        public string FullName => $"{FirstName} {LastName}";

        public virtual ICollection<EmployeeSalaryTransaction> SalaryTransactions { get; set; } = new List<EmployeeSalaryTransaction>();
        public virtual ICollection<EmployeeAttendance> AttendanceRecords { get; set; } = new List<EmployeeAttendance>();
    }
}
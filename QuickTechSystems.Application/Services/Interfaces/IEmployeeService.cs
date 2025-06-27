using QuickTechSystems.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IEmployeeService
    {
        Task<EmployeeDTO?> GetByIdAsync(int id);
        Task<IEnumerable<EmployeeDTO>> GetAllAsync();
        Task<EmployeeDTO> CreateAsync(EmployeeDTO dto);
        Task UpdateAsync(EmployeeDTO dto);
        Task DeleteAsync(int id);
        Task<EmployeeDTO?> GetByUsernameAsync(string username);
        Task ResetPasswordAsync(int employeeId, string newPassword);
        Task UpdateLastLoginAsync(int employeeId);
        Task<bool> ProcessSalaryWithdrawalAsync(int employeeId, decimal amount, string notes);
        Task ProcessAttendancePayrollAsync(int employeeId, IEnumerable<int> attendanceIds, decimal totalAmount);
        Task<IEnumerable<EmployeeSalaryTransactionDTO>> GetSalaryHistoryAsync(int employeeId);
        Task UpdateAsync(int employeeId, EmployeeDTO dto);

        Task<EmployeeAttendanceDTO> CreateAttendanceAsync(EmployeeAttendanceDTO attendanceDto);
        Task<EmployeeAttendanceDTO> UpdateAttendanceAsync(EmployeeAttendanceDTO attendanceDto);
        Task DeleteAttendanceAsync(int attendanceId);
        Task<EmployeeAttendanceDTO?> GetAttendanceByIdAsync(int attendanceId);
        Task<IEnumerable<EmployeeAttendanceDTO>> GetAttendanceByEmployeeAsync(int employeeId);
        Task<IEnumerable<EmployeeAttendanceDTO>> GetAttendanceByDateRangeAsync(int employeeId, DateTime startDate, DateTime endDate);
        Task<AttendanceSummaryDTO> GetMonthlySummaryAsync(int employeeId, int month, int year);
        Task<IEnumerable<AttendanceSummaryDTO>> GetYearlySummaryAsync(int employeeId, int year);
        Task<EmployeeAttendanceDTO?> GetAttendanceByDateAsync(int employeeId, DateTime date);
        Task<IEnumerable<EmployeeAttendanceDTO>> GetAllAttendanceAsync();
        Task<IEnumerable<EmployeeAttendanceDTO>> GetTodayAttendanceAsync();
        Task<IEnumerable<EmployeeAttendanceDTO>> GetUnpaidAttendanceAsync(int employeeId, DateTime startDate, DateTime endDate);
        Task MarkAttendanceAsPaidAsync(IEnumerable<int> attendanceIds);
    }
}
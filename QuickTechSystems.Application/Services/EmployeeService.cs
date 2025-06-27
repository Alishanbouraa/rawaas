using System.Diagnostics;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace QuickTechSystems.Application.Services
{
    public class EmployeeService : IEmployeeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IGenericRepository<Employee> _repository;
        private readonly IGenericRepository<EmployeeAttendance> _attendanceRepository;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDbContextScopeService _dbContextScopeService;
        private readonly IDrawerService _drawerService;
        private readonly IExpenseService _expenseService;
        private readonly Dictionary<string, object> _operationCache;
        private readonly HashSet<string> _processingOperations;

        public EmployeeService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IDrawerService drawerService,
            IExpenseService expenseService,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _repository = unitOfWork.Employees;
            _attendanceRepository = unitOfWork.EmployeeAttendances;
            _eventAggregator = eventAggregator;
            _dbContextScopeService = dbContextScopeService;
            _drawerService = drawerService;
            _expenseService = expenseService;
            _operationCache = new Dictionary<string, object>();
            _processingOperations = new HashSet<string>();
        }

        public async Task<EmployeeDTO?> GetByIdAsync(int id)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var employee = await _repository.Query()
                    .Include(e => e.SalaryTransactions)
                    .FirstOrDefaultAsync(e => e.EmployeeId == id);
                return _mapper.Map<EmployeeDTO>(employee);
            });
        }

        public async Task<IEnumerable<EmployeeDTO>> GetAllAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var employees = await _repository.Query()
                    .Include(e => e.SalaryTransactions)
                    .OrderBy(e => e.FirstName)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<EmployeeDTO>>(employees);
            });
        }

        public async Task<EmployeeDTO> CreateAsync(EmployeeDTO dto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var existingEmployee = await GetByUsernameAsync(dto.Username);
                if (existingEmployee != null)
                {
                    throw new InvalidOperationException("Username already exists");
                }

                var entity = _mapper.Map<Employee>(dto);
                entity.CreatedAt = DateTime.Now;

                var result = await _repository.AddAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                var resultDto = _mapper.Map<EmployeeDTO>(result);
                _eventAggregator.Publish(new EntityChangedEvent<EmployeeDTO>("Create", resultDto));

                return resultDto;
            });
        }

        public async Task UpdateAsync(EmployeeDTO dto)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var entity = _mapper.Map<Employee>(dto);
                await _repository.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync();
                _eventAggregator.Publish(new EntityChangedEvent<EmployeeDTO>("Update", dto));
            });
        }

        public async Task DeleteAsync(int id)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity != null)
                {
                    await _repository.DeleteAsync(entity);
                    await _unitOfWork.SaveChangesAsync();
                    var dto = _mapper.Map<EmployeeDTO>(entity);
                    _eventAggregator.Publish(new EntityChangedEvent<EmployeeDTO>("Delete", dto));
                }
            });
        }

        public async Task<bool> ProcessSalaryWithdrawalAsync(int employeeId, decimal amount, string notes)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var operationKey = $"withdrawal_{employeeId}_{DateTime.Now.Ticks}";

                if (_processingOperations.Contains(operationKey))
                    return false;

                _processingOperations.Add(operationKey);

                try
                {
                    using var transaction = await _unitOfWork.BeginTransactionAsync();

                    var employee = await _repository.GetByIdAsync(employeeId);
                    if (employee == null || amount > employee.CurrentBalance)
                        return false;

                    await _expenseService.CreateEmployeeSalaryExpenseAsync(
                        employeeId,
                        amount,
                        $"{employee.FirstName} {employee.LastName}"
                    );

                    employee.CurrentBalance -= amount;

                    var salaryTransaction = new EmployeeSalaryTransaction
                    {
                        EmployeeId = employeeId,
                        Amount = -amount,
                        TransactionType = "Withdrawal",
                        TransactionDate = DateTime.Now,
                        Notes = notes,
                        Employee = employee
                    };

                    await _unitOfWork.Context.Set<EmployeeSalaryTransaction>().AddAsync(salaryTransaction);
                    await _repository.UpdateAsync(employee);
                    await _unitOfWork.SaveChangesAsync();

                    _eventAggregator.Publish(new EntityChangedEvent<EmployeeDTO>(
                        "Update",
                        _mapper.Map<EmployeeDTO>(employee)
                    ));

                    await transaction.CommitAsync();
                    return true;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    _processingOperations.Remove(operationKey);
                }
            });
        }

        public async Task ProcessAttendancePayrollAsync(int employeeId, IEnumerable<int> attendanceIds, decimal totalAmount)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var operationKey = $"payroll_{employeeId}_{DateTime.Now.Ticks}";

                if (_processingOperations.Contains(operationKey))
                    throw new InvalidOperationException("Payroll processing already in progress for this employee");

                _processingOperations.Add(operationKey);

                try
                {
                    using var transaction = await _unitOfWork.BeginTransactionAsync();

                    var employee = await _repository.GetByIdAsync(employeeId);
                    if (employee == null)
                        throw new InvalidOperationException("Employee not found");

                    var attendanceIdList = attendanceIds.ToList();
                    var attendanceRecords = await _attendanceRepository.Query()
                        .Where(a => attendanceIdList.Contains(a.Id))
                        .ToListAsync();

                    if (!attendanceRecords.Any())
                        throw new InvalidOperationException("No valid attendance records found");

                    var calculatedTotal = attendanceRecords.Sum(a => a.TotalDailyPay);
                    if (Math.Abs(calculatedTotal - totalAmount) > 0.01m)
                        throw new InvalidOperationException("Amount mismatch with attendance calculations");

                    employee.CurrentBalance += totalAmount;

                    var attendanceIdString = string.Join(", ", attendanceIdList);
                    var payrollNotes = $"Payroll for {attendanceRecords.Count} days. Attendance IDs: {attendanceIdString}";

                    var salaryTransaction = new EmployeeSalaryTransaction
                    {
                        EmployeeId = employeeId,
                        Amount = totalAmount,
                        TransactionType = "Payroll",
                        TransactionDate = DateTime.Now,
                        Notes = payrollNotes,
                        Employee = employee
                    };

                    await _unitOfWork.Context.Set<EmployeeSalaryTransaction>().AddAsync(salaryTransaction);
                    await _repository.UpdateAsync(employee);
                    await _unitOfWork.SaveChangesAsync();

                    _eventAggregator.Publish(new EntityChangedEvent<EmployeeDTO>(
                        "Update",
                        _mapper.Map<EmployeeDTO>(employee)
                    ));

                    await transaction.CommitAsync();
                }
                finally
                {
                    _processingOperations.Remove(operationKey);
                }
            });
        }

        public async Task<IEnumerable<EmployeeSalaryTransactionDTO>> GetSalaryHistoryAsync(int employeeId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transactions = await _unitOfWork.Context.Set<EmployeeSalaryTransaction>()
                    .Include(t => t.Employee)
                    .Where(t => t.EmployeeId == employeeId)
                    .OrderByDescending(t => t.TransactionDate)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<EmployeeSalaryTransactionDTO>>(transactions);
            });
        }

        public async Task UpdateAsync(int employeeId, EmployeeDTO dto)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var existingEmployee = await _repository.GetByIdAsync(employeeId);
                if (existingEmployee == null)
                    throw new InvalidOperationException($"Employee with ID {employeeId} not found");

                _mapper.Map(dto, existingEmployee);
                await _repository.UpdateAsync(existingEmployee);
                await _unitOfWork.SaveChangesAsync();
            });
        }

        public async Task<EmployeeDTO?> GetByUsernameAsync(string username)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var employee = await _repository.Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Username == username);
                return _mapper.Map<EmployeeDTO>(employee);
            });
        }

        public async Task ResetPasswordAsync(int employeeId, string newPassword)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var employee = await _repository.GetByIdAsync(employeeId);
                if (employee == null) throw new InvalidOperationException("Employee not found");

                employee.PasswordHash = HashPassword(newPassword);
                await _repository.UpdateAsync(employee);
                await _unitOfWork.SaveChangesAsync();
            });
        }

        public async Task UpdateLastLoginAsync(int employeeId)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var employee = await _repository.GetByIdAsync(employeeId);
                if (employee != null)
                {
                    employee.LastLogin = DateTime.Now;
                    await _repository.UpdateAsync(employee);
                    await _unitOfWork.SaveChangesAsync();
                }
            });
        }

        public async Task<EmployeeAttendanceDTO> CreateAttendanceAsync(EmployeeAttendanceDTO attendanceDto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var existingAttendance = await GetAttendanceByDateAsync(attendanceDto.EmployeeId, attendanceDto.Date);
                if (existingAttendance != null)
                {
                    throw new InvalidOperationException("Attendance record already exists for this date");
                }

                var attendance = _mapper.Map<EmployeeAttendance>(attendanceDto);
                attendance.CreatedAt = DateTime.Now;

                var result = await _attendanceRepository.AddAsync(attendance);
                await _unitOfWork.SaveChangesAsync();

                var resultDto = _mapper.Map<EmployeeAttendanceDTO>(result);
                _eventAggregator.Publish(new EntityChangedEvent<EmployeeAttendanceDTO>("Create", resultDto));

                return resultDto;
            });
        }

        public async Task<EmployeeAttendanceDTO> UpdateAttendanceAsync(EmployeeAttendanceDTO attendanceDto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var existingAttendance = await _attendanceRepository.GetByIdAsync(attendanceDto.Id);
                if (existingAttendance == null)
                    throw new InvalidOperationException("Attendance record not found");

                _mapper.Map(attendanceDto, existingAttendance);
                existingAttendance.UpdatedAt = DateTime.Now;

                await _attendanceRepository.UpdateAsync(existingAttendance);
                await _unitOfWork.SaveChangesAsync();

                var resultDto = _mapper.Map<EmployeeAttendanceDTO>(existingAttendance);
                _eventAggregator.Publish(new EntityChangedEvent<EmployeeAttendanceDTO>("Update", resultDto));

                return resultDto;
            });
        }

        public async Task DeleteAttendanceAsync(int attendanceId)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var attendance = await _attendanceRepository.GetByIdAsync(attendanceId);
                if (attendance != null)
                {
                    await _attendanceRepository.DeleteAsync(attendance);
                    await _unitOfWork.SaveChangesAsync();

                    var dto = _mapper.Map<EmployeeAttendanceDTO>(attendance);
                    _eventAggregator.Publish(new EntityChangedEvent<EmployeeAttendanceDTO>("Delete", dto));
                }
            });
        }

        public async Task<EmployeeAttendanceDTO?> GetAttendanceByIdAsync(int attendanceId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var attendance = await _attendanceRepository.Query()
                    .Include(a => a.Employee)
                    .FirstOrDefaultAsync(a => a.Id == attendanceId);
                return _mapper.Map<EmployeeAttendanceDTO>(attendance);
            });
        }

        public async Task<IEnumerable<EmployeeAttendanceDTO>> GetAttendanceByEmployeeAsync(int employeeId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var attendances = await _attendanceRepository.Query()
                    .Include(a => a.Employee)
                    .Where(a => a.EmployeeId == employeeId)
                    .OrderByDescending(a => a.Date)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<EmployeeAttendanceDTO>>(attendances);
            });
        }

        public async Task<IEnumerable<EmployeeAttendanceDTO>> GetAttendanceByDateRangeAsync(int employeeId, DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var attendances = await _attendanceRepository.Query()
                    .Include(a => a.Employee)
                    .Where(a => a.EmployeeId == employeeId &&
                               a.Date >= startDate.Date &&
                               a.Date <= endDate.Date)
                    .OrderBy(a => a.Date)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<EmployeeAttendanceDTO>>(attendances);
            });
        }

        public async Task<IEnumerable<EmployeeAttendanceDTO>> GetUnpaidAttendanceAsync(int employeeId, DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var paidAttendanceIds = await GetPaidAttendanceIdsAsync(employeeId);

                var unpaidAttendances = await _attendanceRepository.Query()
                    .Include(a => a.Employee)
                    .Where(a => a.EmployeeId == employeeId &&
                               a.Date >= startDate.Date &&
                               a.Date <= endDate.Date &&
                               !paidAttendanceIds.Contains(a.Id))
                    .OrderBy(a => a.Date)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<EmployeeAttendanceDTO>>(unpaidAttendances);
            });
        }

        public async Task MarkAttendanceAsPaidAsync(IEnumerable<int> attendanceIds)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                _operationCache[$"paid_attendance_{DateTime.Now.Ticks}"] = attendanceIds.ToList();
                await _unitOfWork.SaveChangesAsync();
            });
        }

        public async Task<AttendanceSummaryDTO> GetMonthlySummaryAsync(int employeeId, int month, int year)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var attendances = await _attendanceRepository.Query()
                    .Include(a => a.Employee)
                    .Where(a => a.EmployeeId == employeeId &&
                               a.Date >= startDate &&
                               a.Date <= endDate)
                    .ToListAsync();

                var employee = attendances.FirstOrDefault()?.Employee;
                var employeeName = employee != null ? $"{employee.FirstName} {employee.LastName}" : "";

                var summary = new AttendanceSummaryDTO
                {
                    EmployeeId = employeeId,
                    EmployeeName = employeeName,
                    Month = month,
                    Year = year,
                    TotalDaysWorked = attendances.Count,
                    TotalRegularHours = attendances.Sum(a => a.RegularHours),
                    TotalOvertimeHours = attendances.Sum(a => a.OvertimeHours),
                    TotalRegularPay = attendances.Sum(a => a.RegularPay),
                    TotalOvertimePay = attendances.Sum(a => a.OvertimePay)
                };

                summary.GrandTotalSalary = summary.TotalRegularPay + summary.TotalOvertimePay;
                summary.AverageHoursPerDay = summary.TotalDaysWorked > 0 ?
                    (summary.TotalRegularHours + summary.TotalOvertimeHours) / summary.TotalDaysWorked : 0m;
                summary.AverageDailyPay = summary.TotalDaysWorked > 0 ?
                    summary.GrandTotalSalary / summary.TotalDaysWorked : 0m;

                return summary;
            });
        }

        public async Task<IEnumerable<AttendanceSummaryDTO>> GetYearlySummaryAsync(int employeeId, int year)
        {
            var summaries = new List<AttendanceSummaryDTO>();

            for (int month = 1; month <= 12; month++)
            {
                var summary = await GetMonthlySummaryAsync(employeeId, month, year);
                if (summary.TotalDaysWorked > 0)
                {
                    summaries.Add(summary);
                }
            }

            return summaries;
        }

        public async Task<EmployeeAttendanceDTO?> GetAttendanceByDateAsync(int employeeId, DateTime date)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var attendance = await _attendanceRepository.Query()
                    .Include(a => a.Employee)
                    .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date.Date == date.Date);
                return _mapper.Map<EmployeeAttendanceDTO>(attendance);
            });
        }

        public async Task<IEnumerable<EmployeeAttendanceDTO>> GetAllAttendanceAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var attendances = await _attendanceRepository.Query()
                    .Include(a => a.Employee)
                    .OrderByDescending(a => a.Date)
                    .ThenBy(a => a.Employee.FirstName)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<EmployeeAttendanceDTO>>(attendances);
            });
        }

        public async Task<IEnumerable<EmployeeAttendanceDTO>> GetTodayAttendanceAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var today = DateTime.Today;
                var attendances = await _attendanceRepository.Query()
                    .Include(a => a.Employee)
                    .Where(a => a.Date.Date == today)
                    .OrderBy(a => a.Employee.FirstName)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<EmployeeAttendanceDTO>>(attendances);
            });
        }

        private async Task<HashSet<int>> GetPaidAttendanceIdsAsync(int employeeId)
        {
            var paidIds = new HashSet<int>();

            var payrollTransactions = await _unitOfWork.Context.Set<EmployeeSalaryTransaction>()
                .Where(t => t.EmployeeId == employeeId && t.TransactionType == "Payroll")
                .ToListAsync();

            foreach (var transaction in payrollTransactions)
            {
                if (!string.IsNullOrEmpty(transaction.Notes) && transaction.Notes.Contains("Attendance IDs:"))
                {
                    var idsSection = transaction.Notes.Split("Attendance IDs:")[1].Split(',');
                    foreach (var idStr in idsSection)
                    {
                        if (int.TryParse(idStr.Trim(), out var id))
                            paidIds.Add(id);
                    }
                }
            }

            return paidIds;
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
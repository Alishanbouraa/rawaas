using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;

namespace QuickTechSystems.WPF.ViewModels
{
    public class SalaryManagementViewModel : ViewModelBase
    {
        private readonly IEmployeeService _employeeService;
        private readonly EmployeeDTO _employee;
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly Dictionary<string, decimal> _transactionAggregates;
        private readonly Queue<Func<Task>> _pendingOperations;
        private readonly Stack<string> _operationHistory;
        private readonly Dictionary<string, bool> _operationStates;
        private readonly Dictionary<string, AttendanceSummaryDTO> _attendanceSummaryCache;

        private ObservableCollection<EmployeeSalaryTransactionDTO> _salaryTransactions;
        private ObservableCollection<EmployeeAttendanceDTO> _unpaidAttendanceRecords;
        private EmployeeSalaryTransactionDTO _selectedTransaction;
        private decimal _withdrawalAmount;
        private string _withdrawalNotes;
        private bool _isLoading;
        private string _errorMessage;
        private decimal _currentBalance;
        private decimal _totalEarnings;
        private decimal _totalWithdrawals;
        private string _employeeFullName;
        private decimal _monthlySalary;
        private bool _canProcessWithdrawal;
        private decimal _pendingAttendanceEarnings;
        private int _unpaidAttendanceDays;
        private decimal _unpaidRegularHours;
        private decimal _unpaidOvertimeHours;
        private DateTime _selectedPayrollMonth;
        private DateTime _selectedPayrollYear;

        public ObservableCollection<EmployeeSalaryTransactionDTO> SalaryTransactions
        {
            get => _salaryTransactions;
            set => SetProperty(ref _salaryTransactions, value);
        }

        public ObservableCollection<EmployeeAttendanceDTO> UnpaidAttendanceRecords
        {
            get => _unpaidAttendanceRecords;
            set => SetProperty(ref _unpaidAttendanceRecords, value);
        }

        public EmployeeSalaryTransactionDTO SelectedTransaction
        {
            get => _selectedTransaction;
            set => SetProperty(ref _selectedTransaction, value);
        }

        public decimal WithdrawalAmount
        {
            get => _withdrawalAmount;
            set
            {
                if (SetProperty(ref _withdrawalAmount, value))
                    UpdateWithdrawalValidation();
            }
        }

        public string WithdrawalNotes
        {
            get => _withdrawalNotes;
            set => SetProperty(ref _withdrawalNotes, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public decimal CurrentBalance
        {
            get => _currentBalance;
            set => SetProperty(ref _currentBalance, value);
        }

        public decimal TotalEarnings
        {
            get => _totalEarnings;
            set => SetProperty(ref _totalEarnings, value);
        }

        public decimal TotalWithdrawals
        {
            get => _totalWithdrawals;
            set => SetProperty(ref _totalWithdrawals, value);
        }

        public string EmployeeFullName
        {
            get => _employeeFullName;
            private set => SetProperty(ref _employeeFullName, value);
        }

        public decimal MonthlySalary
        {
            get => _monthlySalary;
            private set => SetProperty(ref _monthlySalary, value);
        }

        public bool CanProcessWithdrawal
        {
            get => _canProcessWithdrawal;
            private set => SetProperty(ref _canProcessWithdrawal, value);
        }

        public decimal PendingAttendanceEarnings
        {
            get => _pendingAttendanceEarnings;
            private set => SetProperty(ref _pendingAttendanceEarnings, value);
        }

        public int UnpaidAttendanceDays
        {
            get => _unpaidAttendanceDays;
            private set => SetProperty(ref _unpaidAttendanceDays, value);
        }

        public decimal UnpaidRegularHours
        {
            get => _unpaidRegularHours;
            private set => SetProperty(ref _unpaidRegularHours, value);
        }

        public decimal UnpaidOvertimeHours
        {
            get => _unpaidOvertimeHours;
            private set => SetProperty(ref _unpaidOvertimeHours, value);
        }

        public DateTime SelectedPayrollMonth
        {
            get => _selectedPayrollMonth;
            set
            {
                if (SetProperty(ref _selectedPayrollMonth, value))
                    _ = LoadUnpaidAttendanceAsync();
            }
        }

        public DateTime SelectedPayrollYear
        {
            get => _selectedPayrollYear;
            set
            {
                if (SetProperty(ref _selectedPayrollYear, value))
                    _ = LoadUnpaidAttendanceAsync();
            }
        }

        public bool HasUnpaidAttendance => UnpaidAttendanceRecords?.Any() == true;

        public ICommand ProcessAttendancePayrollCommand { get; private set; }
        public ICommand ProcessWithdrawalCommand { get; private set; }
        public ICommand RefreshDataCommand { get; private set; }
        public ICommand ClearWithdrawalFormCommand { get; private set; }
        public ICommand LoadUnpaidAttendanceCommand { get; private set; }

        public SalaryManagementViewModel(IEmployeeService employeeService, EmployeeDTO employee, IEventAggregator eventAggregator)
            : base(eventAggregator)
        {
            _employeeService = employeeService;
            _employee = employee;
            _operationSemaphore = new SemaphoreSlim(1, 1);
            _transactionAggregates = new Dictionary<string, decimal>();
            _pendingOperations = new Queue<Func<Task>>();
            _operationHistory = new Stack<string>();
            _operationStates = new Dictionary<string, bool>();
            _attendanceSummaryCache = new Dictionary<string, AttendanceSummaryDTO>();

            InitializeProperties(employee);
            InitializeCommands();

            _ = LoadSalaryDataAsync();
        }

        private void InitializeProperties(EmployeeDTO employee)
        {
            _salaryTransactions = new ObservableCollection<EmployeeSalaryTransactionDTO>();
            _unpaidAttendanceRecords = new ObservableCollection<EmployeeAttendanceDTO>();
            _withdrawalNotes = string.Empty;
            _errorMessage = string.Empty;
            _currentBalance = employee.CurrentBalance;
            _employeeFullName = employee?.FullName ?? string.Empty;
            _monthlySalary = employee?.MonthlySalary ?? 0m;
            _selectedPayrollMonth = DateTime.Now;
            _selectedPayrollYear = DateTime.Now;

            UpdateWithdrawalValidation();
        }

        private void InitializeCommands()
        {
            ProcessAttendancePayrollCommand = new AsyncRelayCommand(async _ => await ProcessAttendancePayrollAsync());
            ProcessWithdrawalCommand = new AsyncRelayCommand(async _ => await ProcessWithdrawalAsync());
            RefreshDataCommand = new AsyncRelayCommand(async _ => await LoadSalaryDataAsync());
            ClearWithdrawalFormCommand = new RelayCommand(_ => ClearWithdrawalForm());
            LoadUnpaidAttendanceCommand = new AsyncRelayCommand(async _ => await LoadUnpaidAttendanceAsync());
        }

        private async Task ProcessAttendancePayrollAsync()
        {
            if (!await _operationSemaphore.WaitAsync(100))
            {
                ShowErrorMessage("Payroll processing operation already in progress.");
                return;
            }

            try
            {
                if (!HasUnpaidAttendance)
                {
                    ShowErrorMessage("No unpaid attendance records found for the selected period.");
                    return;
                }

                var confirmationResult = MessageBox.Show(
                    $"Process payroll for {UnpaidAttendanceDays} days ({UnpaidRegularHours:F1} regular hours, {UnpaidOvertimeHours:F1} overtime hours) totaling {PendingAttendanceEarnings:C2}?",
                    "Confirm Payroll Processing",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmationResult != MessageBoxResult.Yes)
                    return;

                IsLoading = true;
                _operationHistory.Push("ProcessPayroll");
                _operationStates["PayrollProcessing"] = true;

                _pendingOperations.Enqueue(() => ExecuteAttendancePayrollProcessingAsync());
                await ExecutePendingOperationsAsync();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Payroll processing error: {ex.Message}");
            }
            finally
            {
                ProcessOperationCompletion("PayrollProcessing");
            }
        }

        private async Task ProcessWithdrawalAsync()
        {
            if (!await _operationSemaphore.WaitAsync(100))
            {
                ShowErrorMessage("Withdrawal operation already in progress.");
                return;
            }

            try
            {
                var validationErrors = ValidateWithdrawalRequest();
                if (validationErrors.Any())
                {
                    ShowErrorMessage(string.Join(", ", validationErrors));
                    return;
                }

                var confirmationResult = MessageBox.Show(
                    $"Process withdrawal of {WithdrawalAmount:C2} for {EmployeeFullName}?",
                    "Confirm Withdrawal",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmationResult != MessageBoxResult.Yes)
                    return;

                IsLoading = true;
                _operationHistory.Push("ProcessWithdrawal");
                _operationStates["WithdrawalProcessing"] = true;

                _pendingOperations.Enqueue(() => ExecuteWithdrawalProcessingAsync());
                await ExecutePendingOperationsAsync();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Withdrawal processing error: {ex.Message}");
            }
            finally
            {
                ProcessOperationCompletion("WithdrawalProcessing");
            }
        }

        private async Task ExecuteAttendancePayrollProcessingAsync()
        {
            var attendanceIds = UnpaidAttendanceRecords.Select(a => a.Id).ToList();
            var totalEarnings = UnpaidAttendanceRecords.Sum(a => a.TotalDailyPay);

            await _employeeService.ProcessAttendancePayrollAsync(_employee.EmployeeId, attendanceIds, totalEarnings);

            await RefreshEmployeeBalance();
            await LoadSalaryDataAsync();
            await LoadUnpaidAttendanceAsync();

            ShowSuccessMessage($"Payroll processed successfully. {totalEarnings:C2} added to employee balance.");
        }

        private async Task ExecuteWithdrawalProcessingAsync()
        {
            var success = await _employeeService.ProcessSalaryWithdrawalAsync(
                _employee.EmployeeId,
                WithdrawalAmount,
                WithdrawalNotes ?? string.Empty);

            if (success)
            {
                await RefreshEmployeeBalance();
                await LoadSalaryDataAsync();
                ClearWithdrawalForm();
                ShowSuccessMessage("Withdrawal processed successfully");
            }
            else
            {
                ShowErrorMessage("Withdrawal processing failed. Insufficient balance or invalid operation.");
            }
        }

        private async Task ExecutePendingOperationsAsync()
        {
            var operationsSnapshot = new Queue<Func<Task>>(_pendingOperations);
            _pendingOperations.Clear();

            while (operationsSnapshot.Count > 0)
            {
                var operation = operationsSnapshot.Dequeue();
                await operation.Invoke();
            }
        }

        private async Task LoadSalaryDataAsync()
        {
            if (!await _operationSemaphore.WaitAsync(100))
                return;

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                _operationHistory.Push("LoadData");

                var transactions = await _employeeService.GetSalaryHistoryAsync(_employee.EmployeeId);
                var transactionList = transactions.OrderByDescending(t => t.TransactionDate).ToList();

                SalaryTransactions = new ObservableCollection<EmployeeSalaryTransactionDTO>(transactionList);

                CalculateTransactionAggregates(transactionList);
                await RefreshEmployeeBalance();
                await LoadUnpaidAttendanceAsync();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error loading salary data: {ex.Message}");
            }
            finally
            {
                ProcessOperationCompletion("LoadData");
            }
        }

        private async Task LoadUnpaidAttendanceAsync()
        {
            try
            {
                var startDate = new DateTime(SelectedPayrollYear.Year, SelectedPayrollMonth.Month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var allAttendance = await _employeeService.GetAttendanceByDateRangeAsync(
                    _employee.EmployeeId, startDate, endDate);

                var paidAttendanceIds = GetPaidAttendanceIds();
                var unpaidAttendance = allAttendance.Where(a => !paidAttendanceIds.Contains(a.Id)).ToList();

                UnpaidAttendanceRecords = new ObservableCollection<EmployeeAttendanceDTO>(unpaidAttendance);

                CalculateUnpaidAttendanceMetrics(unpaidAttendance);
                OnPropertyChanged(nameof(HasUnpaidAttendance));
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error loading unpaid attendance: {ex.Message}");
            }
        }

        private HashSet<int> GetPaidAttendanceIds()
        {
            var paidIds = new HashSet<int>();

            foreach (var transaction in SalaryTransactions.Where(t => t.TransactionType == "Payroll"))
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

        private void CalculateUnpaidAttendanceMetrics(IEnumerable<EmployeeAttendanceDTO> unpaidAttendance)
        {
            var attendanceList = unpaidAttendance.ToList();

            UnpaidAttendanceDays = attendanceList.Count;
            UnpaidRegularHours = attendanceList.Sum(a => a.RegularHours);
            UnpaidOvertimeHours = attendanceList.Sum(a => a.OvertimeHours);
            PendingAttendanceEarnings = attendanceList.Sum(a => a.TotalDailyPay);
        }

        private async Task RefreshEmployeeBalance()
        {
            try
            {
                var updatedEmployee = await _employeeService.GetByIdAsync(_employee.EmployeeId);
                if (updatedEmployee != null)
                {
                    CurrentBalance = updatedEmployee.CurrentBalance;
                    _employee.CurrentBalance = updatedEmployee.CurrentBalance;
                    UpdateWithdrawalValidation();
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error refreshing balance: {ex.Message}");
            }
        }

        private void CalculateTransactionAggregates(IEnumerable<EmployeeSalaryTransactionDTO> transactions)
        {
            _transactionAggregates.Clear();

            var transactionList = transactions.ToList();
            var earnings = transactionList.Where(t => t.Amount > 0).Sum(t => t.Amount);
            var withdrawals = Math.Abs(transactionList.Where(t => t.Amount < 0).Sum(t => t.Amount));

            _transactionAggregates["Earnings"] = earnings;
            _transactionAggregates["Withdrawals"] = withdrawals;
            _transactionAggregates["Net"] = earnings - withdrawals;

            TotalEarnings = earnings;
            TotalWithdrawals = withdrawals;
        }

        private List<string> ValidateWithdrawalRequest()
        {
            var errors = new List<string>();

            if (WithdrawalAmount <= 0)
                errors.Add("Withdrawal amount must be greater than zero");

            if (WithdrawalAmount > CurrentBalance)
                errors.Add("Withdrawal amount exceeds current balance");

            if (WithdrawalAmount > 10000)
                errors.Add("Withdrawal amount exceeds maximum limit");

            return errors;
        }

        private void UpdateWithdrawalValidation()
        {
            CanProcessWithdrawal = WithdrawalAmount > 0 && WithdrawalAmount <= CurrentBalance;
        }

        private void ClearWithdrawalForm()
        {
            WithdrawalAmount = 0;
            WithdrawalNotes = string.Empty;
            UpdateWithdrawalValidation();
        }

        private void ProcessOperationCompletion(string operationType)
        {
            if (_operationHistory.Count > 0)
                _operationHistory.Pop();

            if (_operationStates.ContainsKey(operationType))
                _operationStates[operationType] = false;

            IsLoading = false;
            _operationSemaphore.Release();
        }

        protected override async Task LoadDataAsync()
        {
            await LoadSalaryDataAsync();
        }

        private void ShowErrorMessage(string message)
        {
            ErrorMessage = message;
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            Task.Run(async () =>
            {
                await Task.Delay(5000);
                if (ErrorMessage == message)
                    ErrorMessage = string.Empty;
            });
        }

        private void ShowSuccessMessage(string message)
        {
            MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public override void Dispose()
        {
            _operationSemaphore?.Dispose();
            base.Dispose();
        }
    }
}
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
    public class AttendanceManagementViewModel : ViewModelBase
    {
        private readonly IEmployeeService _employeeService;
        private readonly EmployeeDTO _employee;
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly Dictionary<DateTime, EmployeeAttendanceDTO> _attendanceCache;
        private readonly Stack<string> _operationHistory;
        private readonly Dictionary<string, object> _calculationCache;
        private readonly Queue<Func<Task>> _pendingOperations;

        private ObservableCollection<EmployeeAttendanceDTO> _attendanceRecords;
        private EmployeeAttendanceDTO _selectedAttendance;
        private DateTime _selectedDate;
        private TimeSpan _startTime;
        private TimeSpan _endTime;
        private decimal _hourlyRate;
        private decimal _standardDailyHours;
        private decimal _overtimeMultiplier;
        private bool _isLoading;
        private string _errorMessage;
        private bool _isEditMode;
        private string _employeeFullName;

        public ObservableCollection<EmployeeAttendanceDTO> AttendanceRecords
        {
            get => _attendanceRecords;
            set => SetProperty(ref _attendanceRecords, value);
        }

        public EmployeeAttendanceDTO SelectedAttendance
        {
            get => _selectedAttendance;
            set
            {
                if (SetProperty(ref _selectedAttendance, value) && value != null)
                {
                    LoadAttendanceForEdit(value);
                }
            }
        }

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    UpdateCalculatedValues();
                    CheckExistingAttendance();
                }
            }
        }

        public TimeSpan StartTime
        {
            get => _startTime;
            set
            {
                if (SetProperty(ref _startTime, value))
                    UpdateCalculatedValues();
            }
        }

        public TimeSpan EndTime
        {
            get => _endTime;
            set
            {
                if (SetProperty(ref _endTime, value))
                    UpdateCalculatedValues();
            }
        }

        public decimal HourlyRate
        {
            get => _hourlyRate;
            set
            {
                if (SetProperty(ref _hourlyRate, value))
                    UpdateCalculatedValues();
            }
        }

        public decimal StandardDailyHours
        {
            get => _standardDailyHours;
            set
            {
                if (SetProperty(ref _standardDailyHours, value))
                    UpdateCalculatedValues();
            }
        }

        public decimal OvertimeMultiplier
        {
            get => _overtimeMultiplier;
            set
            {
                if (SetProperty(ref _overtimeMultiplier, value))
                    UpdateCalculatedValues();
            }
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

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public string EmployeeFullName
        {
            get => _employeeFullName;
            private set => SetProperty(ref _employeeFullName, value);
        }

        public decimal CalculatedTotalHours
        {
            get
            {
                if (EndTime > StartTime)
                    return (decimal)(EndTime - StartTime).TotalHours;
                return 0m;
            }
        }

        public decimal CalculatedOvertimeHours
        {
            get
            {
                var totalHours = CalculatedTotalHours;
                return totalHours > StandardDailyHours ? totalHours - StandardDailyHours : 0m;
            }
        }

        public decimal CalculatedRegularPay
        {
            get
            {
                var regularHours = Math.Min(CalculatedTotalHours, StandardDailyHours);
                return regularHours * HourlyRate;
            }
        }

        public decimal CalculatedOvertimePay
        {
            get => CalculatedOvertimeHours * HourlyRate * OvertimeMultiplier;
        }

        public decimal CalculatedTotalPay
        {
            get => CalculatedRegularPay + CalculatedOvertimePay;
        }

        public ICommand SaveAttendanceCommand { get; private set; }
        public ICommand DeleteAttendanceCommand { get; private set; }
        public ICommand ClearFormCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand LoadAttendanceForEditCommand { get; private set; }

        public AttendanceManagementViewModel(IEmployeeService employeeService, EmployeeDTO employee, IEventAggregator eventAggregator)
            : base(eventAggregator)
        {
            _employeeService = employeeService;
            _employee = employee;
            _operationSemaphore = new SemaphoreSlim(1, 1);
            _attendanceCache = new Dictionary<DateTime, EmployeeAttendanceDTO>();
            _operationHistory = new Stack<string>();
            _calculationCache = new Dictionary<string, object>();
            _pendingOperations = new Queue<Func<Task>>();

            _attendanceRecords = new ObservableCollection<EmployeeAttendanceDTO>();
            _selectedDate = DateTime.Today;
            _startTime = new TimeSpan(8, 0, 0);
            _endTime = new TimeSpan(17, 0, 0);
            _hourlyRate = 10.0m;
            _standardDailyHours = 8.0m;
            _overtimeMultiplier = 1.5m;
            _errorMessage = string.Empty;
            _employeeFullName = employee?.FullName ?? string.Empty;

            SaveAttendanceCommand = new AsyncRelayCommand(async _ => await SaveAttendanceAsync());
            DeleteAttendanceCommand = new AsyncRelayCommand(async _ => await DeleteAttendanceAsync());
            ClearFormCommand = new RelayCommand(_ => ClearForm());
            RefreshCommand = new AsyncRelayCommand(async _ => await LoadAttendanceRecordsAsync());
            LoadAttendanceForEditCommand = new RelayCommand(param =>
            {
                if (param is EmployeeAttendanceDTO attendance)
                    LoadAttendanceForEdit(attendance);
            });

            _ = LoadAttendanceRecordsAsync();
        }

        private async Task SaveAttendanceAsync()
        {
            if (!await _operationSemaphore.WaitAsync(100))
            {
                ShowErrorMessage("Operation in progress. Please wait.");
                return;
            }

            try
            {
                IsLoading = true;
                _operationHistory.Push("Save");

                var validationErrors = ValidateAttendanceData();
                if (validationErrors.Any())
                {
                    ShowErrorMessage(string.Join(", ", validationErrors));
                    return;
                }

                var attendanceDto = CreateAttendanceDto();

                if (IsEditMode && SelectedAttendance != null)
                {
                    attendanceDto.Id = SelectedAttendance.Id;
                    await _employeeService.UpdateAttendanceAsync(attendanceDto);
                    ShowSuccessMessage("Attendance updated successfully");
                }
                else
                {
                    await _employeeService.CreateAttendanceAsync(attendanceDto);
                    ShowSuccessMessage("Attendance created successfully");
                }

                await ExecutePendingOperations();
                await LoadAttendanceRecordsAsync();
                ClearForm();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error saving attendance: {ex.Message}");
            }
            finally
            {
                ProcessOperationCompletion();
            }
        }

        private async Task DeleteAttendanceAsync()
        {
            if (SelectedAttendance == null)
            {
                ShowErrorMessage("No attendance record selected.");
                return;
            }

            if (!await _operationSemaphore.WaitAsync(100))
            {
                ShowErrorMessage("Operation in progress. Please wait.");
                return;
            }

            try
            {
                var result = MessageBox.Show(
                    $"Delete attendance record for {SelectedAttendance.Date:MM/dd/yyyy}?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    IsLoading = true;
                    _operationHistory.Push("Delete");

                    await _employeeService.DeleteAttendanceAsync(SelectedAttendance.Id);

                    _attendanceCache.Remove(SelectedAttendance.Date.Date);
                    await LoadAttendanceRecordsAsync();
                    ClearForm();

                    ShowSuccessMessage("Attendance deleted successfully");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error deleting attendance: {ex.Message}");
            }
            finally
            {
                ProcessOperationCompletion();
            }
        }

        private void LoadAttendanceForEdit(EmployeeAttendanceDTO attendance)
        {
            SelectedDate = attendance.Date;
            StartTime = attendance.StartTime;
            EndTime = attendance.EndTime;
            HourlyRate = attendance.HourlyRate;
            StandardDailyHours = attendance.StandardDailyHours;
            OvertimeMultiplier = attendance.OvertimeMultiplier;
            IsEditMode = true;
        }

        private void ClearForm()
        {
            SelectedDate = DateTime.Today;
            StartTime = new TimeSpan(8, 0, 0);
            EndTime = new TimeSpan(17, 0, 0);
            HourlyRate = 10.0m;
            StandardDailyHours = 8.0m;
            OvertimeMultiplier = 1.5m;
            IsEditMode = false;
            SelectedAttendance = null;
            ClearCalculationCache();
        }

        private async Task LoadAttendanceRecordsAsync()
        {
            if (!await _operationSemaphore.WaitAsync(100))
                return;

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var attendances = await _employeeService.GetAttendanceByEmployeeAsync(_employee.EmployeeId);
                var attendanceList = attendances.OrderByDescending(a => a.Date).ToList();

                AttendanceRecords = new ObservableCollection<EmployeeAttendanceDTO>(attendanceList);

                RebuildAttendanceCache(attendanceList);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error loading attendance records: {ex.Message}");
            }
            finally
            {
                ProcessOperationCompletion();
            }
        }

        private void CheckExistingAttendance()
        {
            if (_attendanceCache.TryGetValue(SelectedDate.Date, out var existingAttendance))
            {
                SelectedAttendance = existingAttendance;
            }
        }

        private void UpdateCalculatedValues()
        {
            ClearCalculationCache();
            OnPropertyChanged(nameof(CalculatedTotalHours));
            OnPropertyChanged(nameof(CalculatedOvertimeHours));
            OnPropertyChanged(nameof(CalculatedRegularPay));
            OnPropertyChanged(nameof(CalculatedOvertimePay));
            OnPropertyChanged(nameof(CalculatedTotalPay));
        }

        private List<string> ValidateAttendanceData()
        {
            var errors = new List<string>();

            if (EndTime <= StartTime)
                errors.Add("End time must be after start time");

            if (HourlyRate <= 0)
                errors.Add("Hourly rate must be greater than zero");

            if (StandardDailyHours <= 0)
                errors.Add("Standard daily hours must be greater than zero");

            return errors;
        }

        private EmployeeAttendanceDTO CreateAttendanceDto()
        {
            return new EmployeeAttendanceDTO
            {
                EmployeeId = _employee.EmployeeId,
                Date = SelectedDate.Date,
                StartTime = StartTime,
                EndTime = EndTime,
                HourlyRate = HourlyRate,
                StandardDailyHours = StandardDailyHours,
                OvertimeMultiplier = OvertimeMultiplier
            };
        }

        private void RebuildAttendanceCache(IEnumerable<EmployeeAttendanceDTO> attendanceList)
        {
            _attendanceCache.Clear();
            foreach (var attendance in attendanceList)
            {
                _attendanceCache[attendance.Date.Date] = attendance;
            }
        }

        private void ClearCalculationCache()
        {
            _calculationCache.Clear();
        }

        private async Task ExecutePendingOperations()
        {
            while (_pendingOperations.Count > 0)
            {
                var operation = _pendingOperations.Dequeue();
                await operation.Invoke();
            }
        }

        private void ProcessOperationCompletion()
        {
            if (_operationHistory.Count > 0)
                _operationHistory.Pop();

            IsLoading = false;
            _operationSemaphore.Release();
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

        protected override async Task LoadDataAsync()
        {
            await LoadAttendanceRecordsAsync();
        }

        public override void Dispose()
        {
            _operationSemaphore?.Dispose();
            base.Dispose();
        }
    }
}
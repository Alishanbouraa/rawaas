using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;
using QuickTechSystems.WPF.Views;

namespace QuickTechSystems.WPF.ViewModels
{
    public class EmployeeViewModel : ViewModelBase
    {
        private readonly IEmployeeService _employeeService;
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly Dictionary<string, object> _windowCache;
        private readonly Queue<string> _operationQueue;
        private readonly Dictionary<string, bool> _operationStates;
        private readonly Stack<string> _operationHistory;

        private ObservableCollection<EmployeeDTO> _employees;
        private EmployeeDTO _selectedEmployee;
        private EmployeeDTO _currentEmployee;
        private bool _isNewEmployee;
        private bool _isLoading;
        private string _errorMessage;

        public ObservableCollection<EmployeeDTO> Employees
        {
            get => _employees;
            set => SetProperty(ref _employees, value);
        }

        public EmployeeDTO SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (SetProperty(ref _selectedEmployee, value) && value != null)
                {
                    CurrentEmployee = CreateEmployeeDataTransferObject(value);
                    IsNewEmployee = false;
                }
            }
        }

        public EmployeeDTO CurrentEmployee
        {
            get => _currentEmployee;
            set => SetProperty(ref _currentEmployee, value);
        }

        public bool IsNewEmployee
        {
            get => _isNewEmployee;
            set => SetProperty(ref _isNewEmployee, value);
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

        public ObservableCollection<string> Roles { get; private set; }

        public ICommand AddCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand ResetPasswordCommand { get; private set; }
        public ICommand ManageAttendanceCommand { get; private set; }
        public ICommand ManageSalaryCommand { get; private set; }
        public ICommand ViewSummaryCommand { get; private set; }

        public EmployeeViewModel(IEmployeeService employeeService, IEventAggregator eventAggregator)
            : base(eventAggregator)
        {
            _employeeService = employeeService;
            _operationSemaphore = new SemaphoreSlim(1, 1);
            _windowCache = new Dictionary<string, object>();
            _operationQueue = new Queue<string>();
            _operationStates = new Dictionary<string, bool>();
            _operationHistory = new Stack<string>();

            InitializeCollections();
            InitializeCommands();

            _ = LoadEmployeesAsync();
        }

        private void InitializeCollections()
        {
            _employees = new ObservableCollection<EmployeeDTO>();
            _errorMessage = string.Empty;

            Roles = new ObservableCollection<string> { "Admin", "Manager", "Cashier" };
        }

        private void InitializeCommands()
        {
            AddCommand = new RelayCommand(_ => AddNewEmployee());
            SaveCommand = new AsyncRelayCommand(async param => await SaveEmployeeAsync(param as PasswordBox));
            DeleteCommand = new AsyncRelayCommand(async _ => await DeleteEmployeeAsync());
            ResetPasswordCommand = new AsyncRelayCommand(async _ => await ResetPasswordAsync());
            ManageAttendanceCommand = new RelayCommand(_ => OpenAttendanceManagement());
            ManageSalaryCommand = new RelayCommand(_ => OpenSalaryManagement());
            ViewSummaryCommand = new RelayCommand(_ => OpenMonthlySummary());
        }

        private void AddNewEmployee()
        {
            CurrentEmployee = new EmployeeDTO
            {
                IsActive = true,
                Role = "Cashier",
                MonthlySalary = 0,
                CurrentBalance = 0
            };
            IsNewEmployee = true;
        }

        private async Task SaveEmployeeAsync(PasswordBox passwordBox)
        {
            if (!await ValidateOperationPreconditions("Save"))
                return;

            try
            {
                InitiateLoadingState("Save");

                if (CurrentEmployee == null)
                {
                    ShowErrorMessage("No employee to save.");
                    return;
                }

                var validationErrors = ValidateEmployee(CurrentEmployee, passwordBox);
                if (validationErrors.Any())
                {
                    ShowErrorMessage(string.Join(", ", validationErrors));
                    return;
                }

                await ExecuteSaveOperation(passwordBox);
                await LoadEmployeesAsync();
                IsNewEmployee = false;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error saving employee: {ex.Message}");
            }
            finally
            {
                CompleteOperation("Save");
            }
        }

        private async Task DeleteEmployeeAsync()
        {
            if (CurrentEmployee == null)
            {
                ShowErrorMessage("No employee selected.");
                return;
            }

            if (!await ValidateOperationPreconditions("Delete"))
                return;

            try
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete employee '{CurrentEmployee.FullName}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    InitiateLoadingState("Delete");

                    await _employeeService.DeleteAsync(CurrentEmployee.EmployeeId);
                    await LoadEmployeesAsync();

                    CurrentEmployee = null;
                    ShowSuccessMessage("Employee deleted successfully");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error deleting employee: {ex.Message}");
            }
            finally
            {
                CompleteOperation("Delete");
            }
        }

        private async Task ResetPasswordAsync()
        {
            if (CurrentEmployee == null)
            {
                ShowErrorMessage("No employee selected.");
                return;
            }

            if (!await ValidateOperationPreconditions("ResetPassword"))
                return;

            try
            {
                var dialog = new InputDialog("Reset Password", "Enter new password:");
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Input))
                {
                    InitiateLoadingState("ResetPassword");

                    await _employeeService.ResetPasswordAsync(CurrentEmployee.EmployeeId, dialog.Input);
                    ShowSuccessMessage("Password reset successfully");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error resetting password: {ex.Message}");
            }
            finally
            {
                CompleteOperation("ResetPassword");
            }
        }

        private void OpenAttendanceManagement()
        {
            if (!ValidateEmployeeSelection())
                return;

            var windowKey = $"Attendance_{CurrentEmployee.EmployeeId}";
            var window = GetOrCreateManagedWindow(windowKey, () =>
                CreateAttendanceWindow(windowKey));

            ActivateWindow(window);
        }

        private void OpenSalaryManagement()
        {
            if (!ValidateEmployeeSelection())
                return;

            var windowKey = $"Salary_{CurrentEmployee.EmployeeId}";
            var window = GetOrCreateManagedWindow(windowKey, () =>
                CreateSalaryWindow(windowKey));

            ActivateWindow(window);
        }

        private void OpenMonthlySummary()
        {
            if (!ValidateEmployeeSelection())
                return;

            var windowKey = $"Summary_{CurrentEmployee.EmployeeId}";
            var window = GetOrCreateManagedWindow(windowKey, () =>
                CreateSummaryWindow(windowKey));

            ActivateWindow(window);
        }

        private Window CreateAttendanceWindow(string windowKey)
        {
            var attendanceWindow = new AttendanceManagementWindow
            {
                DataContext = new AttendanceManagementViewModel(_employeeService, CurrentEmployee, _eventAggregator)
            };

            attendanceWindow.Closed += (s, e) => _windowCache.Remove(windowKey);
            return attendanceWindow;
        }

        private Window CreateSalaryWindow(string windowKey)
        {
            var salaryWindow = new SalaryManagementWindow
            {
                DataContext = new SalaryManagementViewModel(_employeeService, CurrentEmployee, _eventAggregator)
            };

            salaryWindow.Closed += (s, e) => _windowCache.Remove(windowKey);
            return salaryWindow;
        }

        private Window CreateSummaryWindow(string windowKey)
        {
            var summaryWindow = new MonthlySummaryWindow
            {
                DataContext = new MonthlySummaryViewModel(_employeeService, CurrentEmployee, _eventAggregator)
            };

            summaryWindow.Closed += (s, e) => _windowCache.Remove(windowKey);
            return summaryWindow;
        }

        private Window GetOrCreateManagedWindow(string windowKey, Func<Window> windowFactory)
        {
            if (!_windowCache.ContainsKey(windowKey))
            {
                _windowCache[windowKey] = windowFactory();
            }

            return _windowCache[windowKey] as Window;
        }

        private void ActivateWindow(Window window)
        {
            window?.Show();
            window?.Activate();
        }

        private bool ValidateEmployeeSelection()
        {
            if (CurrentEmployee == null)
            {
                ShowErrorMessage("Please select an employee first.");
                return false;
            }
            return true;
        }

        private async Task<bool> ValidateOperationPreconditions(string operationType)
        {
            if (!await _operationSemaphore.WaitAsync(100))
            {
                ShowErrorMessage("Operation in progress. Please wait.");
                return false;
            }

            if (_operationStates.GetValueOrDefault(operationType, false))
            {
                ShowErrorMessage($"{operationType} operation already in progress.");
                _operationSemaphore.Release();
                return false;
            }

            return true;
        }

        private void InitiateLoadingState(string operationType)
        {
            IsLoading = true;
            _operationQueue.Enqueue(operationType);
            _operationHistory.Push(operationType);
            _operationStates[operationType] = true;
        }

        private void CompleteOperation(string operationType)
        {
            if (_operationQueue.Count > 0)
                _operationQueue.Dequeue();

            if (_operationHistory.Count > 0)
                _operationHistory.Pop();

            _operationStates[operationType] = false;
            IsLoading = false;
            _operationSemaphore.Release();
        }

        private async Task ExecuteSaveOperation(PasswordBox passwordBox)
        {
            if (IsNewEmployee)
            {
                CurrentEmployee.PasswordHash = HashPassword(passwordBox.Password);
                await _employeeService.CreateAsync(CurrentEmployee);
                ShowSuccessMessage("Employee created successfully");
            }
            else
            {
                await _employeeService.UpdateAsync(CurrentEmployee.EmployeeId, CurrentEmployee);
                ShowSuccessMessage("Employee updated successfully");
            }
        }

        private EmployeeDTO CreateEmployeeDataTransferObject(EmployeeDTO source)
        {
            return new EmployeeDTO
            {
                EmployeeId = source.EmployeeId,
                Username = source.Username,
                FirstName = source.FirstName,
                LastName = source.LastName,
                Role = source.Role,
                IsActive = source.IsActive,
                MonthlySalary = source.MonthlySalary,
                CurrentBalance = source.CurrentBalance
            };
        }

        private List<string> ValidateEmployee(EmployeeDTO employee, PasswordBox passwordBox)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(employee.Username))
                errors.Add("Username is required");

            if (string.IsNullOrWhiteSpace(employee.FirstName))
                errors.Add("First name is required");

            if (string.IsNullOrWhiteSpace(employee.LastName))
                errors.Add("Last name is required");

            if (IsNewEmployee && (passwordBox == null || string.IsNullOrWhiteSpace(passwordBox.Password)))
                errors.Add("Password is required for new employees");

            return errors;
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        protected override async Task LoadDataAsync()
        {
            await LoadEmployeesAsync();
        }

        private async Task LoadEmployeesAsync()
        {
            if (!await _operationSemaphore.WaitAsync(100))
                return;

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var employees = await _employeeService.GetAllAsync();
                var employeeList = employees.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).ToList();

                Employees = new ObservableCollection<EmployeeDTO>(employeeList);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error loading employees: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationSemaphore.Release();
            }
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
            foreach (var window in _windowCache.Values.OfType<Window>())
            {
                window?.Close();
            }
            _windowCache.Clear();

            _operationSemaphore?.Dispose();
            base.Dispose();
        }
    }
}
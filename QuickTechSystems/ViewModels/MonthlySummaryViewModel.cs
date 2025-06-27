using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;

namespace QuickTechSystems.WPF.ViewModels
{
    public class MonthlySummaryViewModel : ViewModelBase
    {
        private readonly IEmployeeService _employeeService;
        private readonly EmployeeDTO _employee;
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly Dictionary<int, AttendanceSummaryDTO> _monthlySummaryCache;
        private readonly SortedDictionary<int, ObservableCollection<AttendanceSummaryDTO>> _yearlySummaryCache;
        private readonly Dictionary<string, decimal> _aggregateMetricsCache;
        private readonly Queue<Func<Task>> _pendingOperations;
        private readonly Stack<string> _operationHistory;

        private AttendanceSummaryDTO _currentMonthlySummary;
        private ObservableCollection<AttendanceSummaryDTO> _yearlySummaries;
        private int _selectedMonth;
        private int _selectedYear;
        private bool _isLoading;
        private string _errorMessage;
        private string _employeeFullName;
        private decimal _totalAnnualEarnings;
        private decimal _averageMonthlyEarnings;
        private int _totalWorkingDays;
        private decimal _totalWorkingHours;

        public AttendanceSummaryDTO CurrentMonthlySummary
        {
            get => _currentMonthlySummary;
            set => SetProperty(ref _currentMonthlySummary, value);
        }

        public ObservableCollection<AttendanceSummaryDTO> YearlySummaries
        {
            get => _yearlySummaries;
            set => SetProperty(ref _yearlySummaries, value);
        }

        public int SelectedMonth
        {
            get => _selectedMonth;
            set
            {
                if (SetProperty(ref _selectedMonth, value))
                    _ = LoadMonthlySummaryAsync();
            }
        }

        public int SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (SetProperty(ref _selectedYear, value))
                {
                    _ = LoadMonthlySummaryAsync();
                    _ = LoadYearlySummaryAsync();
                }
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

        public string EmployeeFullName
        {
            get => _employeeFullName;
            private set => SetProperty(ref _employeeFullName, value);
        }

        public ObservableCollection<int> AvailableMonths { get; private set; }
        public ObservableCollection<int> AvailableYears { get; private set; }

        public decimal TotalAnnualEarnings
        {
            get => _totalAnnualEarnings;
            private set => SetProperty(ref _totalAnnualEarnings, value);
        }

        public decimal AverageMonthlyEarnings
        {
            get => _averageMonthlyEarnings;
            private set => SetProperty(ref _averageMonthlyEarnings, value);
        }

        public int TotalWorkingDays
        {
            get => _totalWorkingDays;
            private set => SetProperty(ref _totalWorkingDays, value);
        }

        public decimal TotalWorkingHours
        {
            get => _totalWorkingHours;
            private set => SetProperty(ref _totalWorkingHours, value);
        }

        public ICommand RefreshDataCommand { get; private set; }
        public ICommand ExportSummaryCommand { get; private set; }
        public ICommand PreviousMonthCommand { get; private set; }
        public ICommand NextMonthCommand { get; private set; }

        public MonthlySummaryViewModel(IEmployeeService employeeService, EmployeeDTO employee, IEventAggregator eventAggregator)
            : base(eventAggregator)
        {
            _employeeService = employeeService;
            _employee = employee;
            _operationSemaphore = new SemaphoreSlim(1, 1);
            _monthlySummaryCache = new Dictionary<int, AttendanceSummaryDTO>();
            _yearlySummaryCache = new SortedDictionary<int, ObservableCollection<AttendanceSummaryDTO>>();
            _aggregateMetricsCache = new Dictionary<string, decimal>();
            _pendingOperations = new Queue<Func<Task>>();
            _operationHistory = new Stack<string>();

            _yearlySummaries = new ObservableCollection<AttendanceSummaryDTO>();
            _selectedMonth = DateTime.Now.Month;
            _selectedYear = DateTime.Now.Year;
            _errorMessage = string.Empty;
            _employeeFullName = employee?.FullName ?? string.Empty;

            InitializeAvailableRanges();
            InitializeCommands();

            _ = LoadInitialDataAsync();
        }

        private void InitializeCommands()
        {
            RefreshDataCommand = new AsyncRelayCommand(async _ => await RefreshAllDataAsync());
            ExportSummaryCommand = new RelayCommand(_ => ExportSummaryData());
            PreviousMonthCommand = new RelayCommand(_ => NavigateToPreviousMonth());
            NextMonthCommand = new RelayCommand(_ => NavigateToNextMonth());
        }

        private void InitializeAvailableRanges()
        {
            var currentYear = DateTime.Now.Year;

            AvailableMonths = new ObservableCollection<int>(Enumerable.Range(1, 12));
            AvailableYears = new ObservableCollection<int>(Enumerable.Range(currentYear - 5, 11));
        }

        private async Task LoadInitialDataAsync()
        {
            if (!await _operationSemaphore.WaitAsync(100))
                return;

            try
            {
                IsLoading = true;
                _operationHistory.Push("InitialLoad");

                await LoadMonthlySummaryAsync();
                await LoadYearlySummaryAsync();
            }
            finally
            {
                ProcessOperationCompletion();
            }
        }

        private async Task LoadMonthlySummaryAsync()
        {
            if (!await _operationSemaphore.WaitAsync(100))
                return;

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;
                _operationHistory.Push("MonthlyLoad");

                var monthKey = SelectedYear * 100 + SelectedMonth;

                if (_monthlySummaryCache.TryGetValue(monthKey, out var cachedSummary))
                {
                    CurrentMonthlySummary = cachedSummary;
                    return;
                }

                var summary = await _employeeService.GetMonthlySummaryAsync(_employee.EmployeeId, SelectedMonth, SelectedYear);

                CurrentMonthlySummary = summary;
                _monthlySummaryCache[monthKey] = summary;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error loading monthly summary: {ex.Message}");
            }
            finally
            {
                ProcessOperationCompletion();
            }
        }

        private async Task LoadYearlySummaryAsync()
        {
            if (!await _operationSemaphore.WaitAsync(100))
                return;

            try
            {
                IsLoading = true;
                _operationHistory.Push("YearlyLoad");

                if (_yearlySummaryCache.TryGetValue(SelectedYear, out var cachedYearlySummaries))
                {
                    YearlySummaries = cachedYearlySummaries;
                    UpdateYearlyAggregates();
                    return;
                }

                var yearlySummaries = await _employeeService.GetYearlySummaryAsync(_employee.EmployeeId, SelectedYear);
                var summaryList = yearlySummaries.OrderBy(s => s.Month).ToList();

                var summaryCollection = new ObservableCollection<AttendanceSummaryDTO>(summaryList);
                YearlySummaries = summaryCollection;
                _yearlySummaryCache[SelectedYear] = summaryCollection;

                UpdateYearlyAggregates();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error loading yearly summary: {ex.Message}");
            }
            finally
            {
                ProcessOperationCompletion();
            }
        }

        private async Task RefreshAllDataAsync()
        {
            if (!await _operationSemaphore.WaitAsync(100))
            {
                ShowErrorMessage("Refresh operation already in progress.");
                return;
            }

            try
            {
                IsLoading = true;
                _operationHistory.Push("RefreshAll");

                ClearAllCaches();

                await LoadMonthlySummaryAsync();
                await LoadYearlySummaryAsync();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error refreshing data: {ex.Message}");
            }
            finally
            {
                ProcessOperationCompletion();
            }
        }

        private void ClearAllCaches()
        {
            _monthlySummaryCache.Clear();
            _yearlySummaryCache.Clear();
            _aggregateMetricsCache.Clear();
        }

        private void UpdateYearlyAggregates()
        {
            if (YearlySummaries?.Any() != true)
            {
                ResetAggregateMetrics();
                return;
            }

            var cacheKey = $"{SelectedYear}_aggregates";
            if (_aggregateMetricsCache.ContainsKey(cacheKey))
            {
                LoadAggregatesFromCache(cacheKey);
                return;
            }

            CalculateAndCacheAggregates(cacheKey);
        }

        private void ResetAggregateMetrics()
        {
            TotalAnnualEarnings = 0m;
            AverageMonthlyEarnings = 0m;
            TotalWorkingDays = 0;
            TotalWorkingHours = 0m;
        }

        private void LoadAggregatesFromCache(string cacheKey)
        {
            TotalAnnualEarnings = _aggregateMetricsCache.GetValueOrDefault($"{cacheKey}_annual", 0m);
            AverageMonthlyEarnings = _aggregateMetricsCache.GetValueOrDefault($"{cacheKey}_monthly", 0m);
            TotalWorkingDays = (int)_aggregateMetricsCache.GetValueOrDefault($"{cacheKey}_days", 0m);
            TotalWorkingHours = _aggregateMetricsCache.GetValueOrDefault($"{cacheKey}_hours", 0m);
        }

        private void CalculateAndCacheAggregates(string cacheKey)
        {
            var annualEarnings = YearlySummaries.Sum(s => s.GrandTotalSalary);
            var summariesWithData = YearlySummaries.Where(s => s.TotalDaysWorked > 0).ToList();
            var monthlyAverage = summariesWithData.Any() ? summariesWithData.Average(s => s.GrandTotalSalary) : 0m;
            var workingDays = YearlySummaries.Sum(s => s.TotalDaysWorked);
            var workingHours = YearlySummaries.Sum(s => s.TotalRegularHours + s.TotalOvertimeHours);

            TotalAnnualEarnings = annualEarnings;
            AverageMonthlyEarnings = monthlyAverage;
            TotalWorkingDays = workingDays;
            TotalWorkingHours = workingHours;

            _aggregateMetricsCache[$"{cacheKey}_annual"] = annualEarnings;
            _aggregateMetricsCache[$"{cacheKey}_monthly"] = monthlyAverage;
            _aggregateMetricsCache[$"{cacheKey}_days"] = workingDays;
            _aggregateMetricsCache[$"{cacheKey}_hours"] = workingHours;
        }

        private void NavigateToPreviousMonth()
        {
            if (SelectedMonth == 1)
            {
                SelectedMonth = 12;
                SelectedYear--;
            }
            else
            {
                SelectedMonth--;
            }
        }

        private void NavigateToNextMonth()
        {
            if (SelectedMonth == 12)
            {
                SelectedMonth = 1;
                SelectedYear++;
            }
            else
            {
                SelectedMonth++;
            }
        }

        private void ExportSummaryData()
        {
            try
            {
                var exportData = PrepareExportData();

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Summary Data",
                    Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt",
                    FileName = $"{EmployeeFullName}_Summary_{SelectedYear}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(saveFileDialog.FileName, exportData);
                    ShowSuccessMessage("Summary data exported successfully");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Export error: {ex.Message}");
            }
        }

        private string PrepareExportData()
        {
            var exportLines = new List<string>
            {
                "Employee Summary Report",
                $"Employee: {EmployeeFullName}",
                $"Year: {SelectedYear}",
                "",
                "Month,Days Worked,Regular Hours,Overtime Hours,Regular Pay,Overtime Pay,Total Pay"
            };

            if (YearlySummaries?.Any() == true)
            {
                var sortedSummaries = YearlySummaries.OrderBy(s => s.Month);
                foreach (var summary in sortedSummaries)
                {
                    exportLines.Add($"{summary.Month},{summary.TotalDaysWorked},{summary.TotalRegularHours:F2}," +
                                  $"{summary.TotalOvertimeHours:F2},{summary.TotalRegularPay:F2}," +
                                  $"{summary.TotalOvertimePay:F2},{summary.GrandTotalSalary:F2}");
                }

                exportLines.Add("");
                exportLines.Add($"Total Annual Earnings: {TotalAnnualEarnings:C2}");
                exportLines.Add($"Average Monthly Earnings: {AverageMonthlyEarnings:C2}");
                exportLines.Add($"Total Working Days: {TotalWorkingDays}");
                exportLines.Add($"Total Working Hours: {TotalWorkingHours:F2}");
            }

            return string.Join(Environment.NewLine, exportLines);
        }

        private void ProcessOperationCompletion()
        {
            if (_operationHistory.Count > 0)
                _operationHistory.Pop();

            IsLoading = false;
            _operationSemaphore.Release();
        }

        protected override async Task LoadDataAsync()
        {
            await LoadInitialDataAsync();
        }

        private void ShowErrorMessage(string message)
        {
            ErrorMessage = message;

            Task.Run(async () =>
            {
                await Task.Delay(5000);
                if (ErrorMessage == message)
                    ErrorMessage = string.Empty;
            });
        }

        private void ShowSuccessMessage(string message)
        {
            System.Windows.MessageBox.Show(message, "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        public override void Dispose()
        {
            _operationSemaphore?.Dispose();
            base.Dispose();
        }
    }
}
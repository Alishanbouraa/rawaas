using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace QuickTechSystems.WPF.Views
{
    public partial class EmployeeView : UserControl
    {
        private readonly Dictionary<string, FrameworkElement> _elementCache;
        private bool _isInitialized;

        public EmployeeView()
        {
            InitializeComponent();
            _elementCache = new Dictionary<string, FrameworkElement>();
            SetupViewBehavior();
        }

        private void SetupViewBehavior()
        {
            Loaded += OnViewLoaded;
            SizeChanged += OnViewSizeChanged;
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            InitializeElementCache();
            ApplyResponsiveLayout();
            _isInitialized = true;
        }

        private void OnViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isInitialized)
            {
                ApplyResponsiveLayout();
            }
        }

        private void InitializeElementCache()
        {
            _elementCache.Clear();

            var mainGrid = Content as Grid;
            if (mainGrid != null)
            {
                _elementCache["MainGrid"] = mainGrid;
            }

            foreach (FrameworkElement child in LogicalTreeHelper.GetChildren(this))
            {
                if (!string.IsNullOrEmpty(child.Name))
                {
                    _elementCache[child.Name] = child;
                }
            }
        }

        private void ApplyResponsiveLayout()
        {
            var parentWindow = Window.GetWindow(this);
            if (parentWindow == null) return;

            var windowWidth = parentWindow.ActualWidth;
            var windowHeight = parentWindow.ActualHeight;

            ApplyWidthBasedLayout(windowWidth);
            ApplyHeightBasedLayout(windowHeight);
        }

        private void ApplyWidthBasedLayout(double windowWidth)
        {
            if (!_elementCache.TryGetValue("MainGrid", out var mainGrid) || !(mainGrid is Grid grid))
                return;

            if (windowWidth < 1200)
            {
                EnsureRowDefinitionsForStackedLayout(grid);
                ConfigureStackedLayout(grid);
            }
            else
            {
                EnsureColumnDefinitionsForSideBySideLayout(grid);
                ConfigureSideBySideLayout(grid);
            }
        }

        private void ApplyHeightBasedLayout(double windowHeight)
        {
            if (windowHeight < 700)
            {
                SetMaxHeightForScrollableContent(windowHeight * 0.75);
            }
            else
            {
                RemoveMaxHeightConstraints();
            }
        }

        private void EnsureRowDefinitionsForStackedLayout(Grid grid)
        {
            if (grid.RowDefinitions.Count < 2)
            {
                grid.RowDefinitions.Clear();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }
        }

        private void EnsureColumnDefinitionsForSideBySideLayout(Grid grid)
        {
            if (grid.ColumnDefinitions.Count < 3)
            {
                grid.ColumnDefinitions.Clear();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
        }

        private void ConfigureStackedLayout(Grid grid)
        {
            foreach (FrameworkElement child in grid.Children)
            {
                if (child is Border border)
                {
                    var currentColumn = Grid.GetColumn(border);
                    if (currentColumn == 2)
                    {
                        Grid.SetColumn(border, 0);
                        Grid.SetRow(border, 1);
                        Grid.SetColumnSpan(border, 1);
                        border.Margin = new Thickness(0, 20, 0, 0);
                    }
                }
            }
        }

        private void ConfigureSideBySideLayout(Grid grid)
        {
            foreach (FrameworkElement child in grid.Children)
            {
                if (child is Border border)
                {
                    var currentRow = Grid.GetRow(border);
                    if (currentRow == 1)
                    {
                        Grid.SetColumn(border, 2);
                        Grid.SetRow(border, 0);
                        Grid.SetColumnSpan(border, 1);
                        border.Margin = new Thickness(0);
                    }
                }
            }
        }

        private void SetMaxHeightForScrollableContent(double maxHeight)
        {
            foreach (var element in _elementCache.Values)
            {
                if (element is ScrollViewer scrollViewer)
                {
                    scrollViewer.MaxHeight = maxHeight;
                }
                else if (element is Border border && border.Child is ScrollViewer childScrollViewer)
                {
                    childScrollViewer.MaxHeight = maxHeight;
                }
            }
        }

        private void RemoveMaxHeightConstraints()
        {
            foreach (var element in _elementCache.Values)
            {
                if (element is ScrollViewer scrollViewer)
                {
                    scrollViewer.ClearValue(ScrollViewer.MaxHeightProperty);
                }
                else if (element is Border border && border.Child is ScrollViewer childScrollViewer)
                {
                    childScrollViewer.ClearValue(ScrollViewer.MaxHeightProperty);
                }
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (_isInitialized && (sizeInfo.WidthChanged || sizeInfo.HeightChanged))
            {
                ApplyResponsiveLayout();
            }
        }
    }
}
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace QuickTechSystems.WPF.Views
{
    public partial class InputDialog : Window, INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _prompt = string.Empty;
        private string _input = string.Empty;
        private FlowDirection _flowDirection = FlowDirection.LeftToRight;

        public new string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Prompt
        {
            get => _prompt;
            set => SetProperty(ref _prompt, value);
        }

        public string Input
        {
            get => _input;
            set => SetProperty(ref _input, value);
        }

        public new FlowDirection FlowDirection
        {
            get => _flowDirection;
            set => SetProperty(ref _flowDirection, value);
        }

        public bool IsInputValid => !string.IsNullOrWhiteSpace(Input);

        public InputDialog()
        {
            InitializeComponent();
            DataContext = this;
            SetupKeyboardNavigation();
            ConfigureWindowBehavior();
        }

        public InputDialog(string title, string prompt) : this()
        {
            Title = title ?? string.Empty;
            Prompt = prompt ?? string.Empty;
        }

        public InputDialog(string title, string prompt, string initialInput) : this(title, prompt)
        {
            Input = initialInput ?? string.Empty;
        }

        private void SetupKeyboardNavigation()
        {
            KeyDown += OnKeyDown;
            Loaded += OnWindowLoaded;
        }

        private void ConfigureWindowBehavior()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Topmost = false;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            }));
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter when IsInputValid:
                    e.Handled = true;
                    DialogResult = true;
                    break;

                case Key.Escape:
                    e.Handled = true;
                    DialogResult = false;
                    break;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInput())
            {
                DialogResult = true;
            }
        }

        private bool ValidateInput()
        {
            if (!IsInputValid)
            {
                ShowValidationError("Input cannot be empty or whitespace only.");
                return false;
            }

            return true;
        }

        private void ShowValidationError(string message)
        {
            MessageBox.Show(
                message,
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning,
                MessageBoxResult.OK,
                MessageBoxOptions.None);

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            }));
        }

        protected virtual bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.Focus();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (DialogResult == null)
            {
                DialogResult = false;
            }
            base.OnClosing(e);
        }
    }
}
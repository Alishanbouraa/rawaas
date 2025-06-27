using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QuickTechSystems.WPF.Views;

namespace QuickTechSystems.WPF.Services
{
    public static class InputDialogService
    {
        private static readonly Dictionary<string, Window> _activeDialogs = new Dictionary<string, Window>();
        private static readonly SemaphoreSlim _dialogSemaphore = new SemaphoreSlim(1, 1);
        private static readonly Stack<DialogResult> _dialogHistory = new Stack<DialogResult>();

        public static bool TryGetDecimalInput(string title, string productName, decimal initialValue, out decimal result, Window owner = null)
        {
            result = 0;
            var dialogKey = $"{title}_{productName}_{Thread.CurrentThread.ManagedThreadId}";

            if (!_dialogSemaphore.Wait(100))
            {
                return false;
            }

            try
            {
                if (_activeDialogs.ContainsKey(dialogKey))
                {
                    _activeDialogs[dialogKey].Activate();
                    return false;
                }

                var validationRules = new Dictionary<string, Func<decimal, bool>>
                {
                    ["positive"] = value => value > 0,
                    ["range"] = value => value >= 0.01m && value <= 999999.99m,
                    ["precision"] = value => decimal.Round(value, 2) == value
                };

                var window = CreateDecimalInputWindow(title, productName, initialValue, validationRules, owner);
                _activeDialogs[dialogKey] = window;

                decimal capturedResult = 0;
                window.Closed += (s, e) =>
                {
                    _activeDialogs.Remove(dialogKey);
                    _dialogHistory.Push(new DialogResult { Success = window.DialogResult == true, Value = capturedResult });
                };

                var dialogResult = window.ShowDialog();

                if (dialogResult == true && window.Tag is decimal validatedValue)
                {
                    capturedResult = validatedValue;
                    result = validatedValue;
                    return true;
                }

                return false;
            }
            finally
            {
                _dialogSemaphore.Release();
            }
        }

        public static bool TryGetStringInput(string title, string prompt, string initialValue, out string result, Window owner = null)
        {
            result = string.Empty;

            if (!_dialogSemaphore.Wait(100))
            {
                return false;
            }

            try
            {
                var dialog = new InputDialog(title, prompt)
                {
                    Owner = owner ?? System.Windows.Application.Current?.MainWindow,
                    Input = initialValue ?? string.Empty
                };

                if (dialog.ShowDialog() == true)
                {
                    result = dialog.Input;
                    return !string.IsNullOrWhiteSpace(result);
                }

                return false;
            }
            finally
            {
                _dialogSemaphore.Release();
            }
        }

        public static bool TryGetPasswordInput(string title, string prompt, out string result, Window owner = null)
        {
            result = string.Empty;

            if (!_dialogSemaphore.Wait(100))
            {
                return false;
            }

            try
            {
                var window = CreatePasswordInputWindow(title, prompt, owner);

                if (window.ShowDialog() == true && window.Tag is string password)
                {
                    result = password;
                    return !string.IsNullOrWhiteSpace(result);
                }

                return false;
            }
            finally
            {
                _dialogSemaphore.Release();
            }
        }

        private static Window CreateDecimalInputWindow(string title, string productName, decimal initialValue,
            Dictionary<string, Func<decimal, bool>> validationRules, Window owner)
        {
            var window = new Window
            {
                Title = title,
                Width = 320,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner ?? System.Windows.Application.Current?.MainWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB")),
                ShowInTaskbar = false
            };

            var mainContainer = CreateMainContainer();
            var contentGrid = CreateContentGrid();

            var productNameBlock = CreateProductNameBlock(productName);
            var inputLabel = CreateInputLabel(title);
            var inputTextBox = CreateDecimalInputTextBox(initialValue);
            var buttonsPanel = CreateButtonsPanel(window, inputTextBox, validationRules);

            contentGrid.Children.Add(productNameBlock);
            contentGrid.Children.Add(inputLabel);
            contentGrid.Children.Add(inputTextBox);
            contentGrid.Children.Add(buttonsPanel);

            Grid.SetRow(productNameBlock, 0);
            Grid.SetRow(inputLabel, 1);
            Grid.SetRow(inputTextBox, 2);
            Grid.SetRow(buttonsPanel, 3);

            mainContainer.Child = contentGrid;
            window.Content = mainContainer;

            ConfigureWindowEvents(window, inputTextBox);

            return window;
        }

        private static Window CreatePasswordInputWindow(string title, string prompt, Window owner)
        {
            var window = new Window
            {
                Title = title,
                Width = 300,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner ?? System.Windows.Application.Current?.MainWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFB")),
                ShowInTaskbar = false
            };

            var mainContainer = CreateMainContainer();
            var contentGrid = CreateContentGrid();

            var promptBlock = CreatePromptBlock(prompt);
            var passwordBox = CreatePasswordBox();
            var buttonsPanel = CreatePasswordButtonsPanel(window, passwordBox);

            contentGrid.Children.Add(promptBlock);
            contentGrid.Children.Add(passwordBox);
            contentGrid.Children.Add(buttonsPanel);

            Grid.SetRow(promptBlock, 0);
            Grid.SetRow(passwordBox, 1);
            Grid.SetRow(buttonsPanel, 2);

            mainContainer.Child = contentGrid;
            window.Content = mainContainer;

            window.Loaded += (s, e) => passwordBox.Focus();

            return window;
        }

        private static Border CreateMainContainer()
        {
            return new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 20,
                    Direction = 270,
                    ShadowDepth = 4,
                    Opacity = 0.15
                }
            };
        }

        private static Grid CreateContentGrid()
        {
            var grid = new Grid { Margin = new Thickness(24) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            return grid;
        }

        private static TextBlock CreateProductNameBlock(string productName)
        {
            return new TextBlock
            {
                Text = productName,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2937")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            };
        }

        private static TextBlock CreateInputLabel(string title)
        {
            return new TextBlock
            {
                Text = title.Contains("Quantity") ? "Enter quantity:" : "Enter price:",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                Margin = new Thickness(0, 0, 0, 8)
            };
        }

        private static TextBlock CreatePromptBlock(string prompt)
        {
            return new TextBlock
            {
                Text = prompt,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static TextBox CreateDecimalInputTextBox(decimal initialValue)
        {
            var textBox = new TextBox
            {
                Text = initialValue.ToString(CultureInfo.CurrentCulture),
                Margin = new Thickness(0, 0, 0, 20),
                Padding = new Thickness(12),
                FontSize = 16,
                Height = 40,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")),
                BorderThickness = new Thickness(1)
            };

            textBox.GotFocus += (s, e) => textBox.SelectAll();
            textBox.PreviewTextInput += (s, e) => e.Handled = !IsValidDecimalInput(e.Text, textBox.Text, textBox.SelectionStart, textBox.SelectionLength);

            return textBox;
        }

        private static PasswordBox CreatePasswordBox()
        {
            return new PasswordBox
            {
                Margin = new Thickness(0, 0, 0, 20),
                Padding = new Thickness(12),
                FontSize = 14,
                Height = 40,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1D5DB")),
                BorderThickness = new Thickness(1)
            };
        }

        private static StackPanel CreateButtonsPanel(Window window, TextBox inputTextBox, Dictionary<string, Func<decimal, bool>> validationRules)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelButton = CreateButton("Cancel", "#6B7280", "#4B5563");
            var okButton = CreateButton("OK", "#3B82F6", "#2563EB");

            cancelButton.Click += (s, e) => window.DialogResult = false;
            okButton.Click += (s, e) => HandleDecimalOkClick(window, inputTextBox, validationRules);

            cancelButton.Margin = new Thickness(0, 0, 12, 0);
            panel.Children.Add(cancelButton);
            panel.Children.Add(okButton);

            return panel;
        }

        private static StackPanel CreatePasswordButtonsPanel(Window window, PasswordBox passwordBox)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelButton = CreateButton("Cancel", "#6B7280", "#4B5563");
            var okButton = CreateButton("OK", "#3B82F6", "#2563EB");

            cancelButton.Click += (s, e) => window.DialogResult = false;
            okButton.Click += (s, e) => HandlePasswordOkClick(window, passwordBox);

            cancelButton.Margin = new Thickness(0, 0, 12, 0);
            panel.Children.Add(cancelButton);
            panel.Children.Add(okButton);

            return panel;
        }

        private static Button CreateButton(string content, string backgroundColor, string hoverColor)
        {
            var button = new Button
            {
                Content = content,
                Width = 80,
                Height = 32,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgroundColor)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };

            var style = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));

            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            border.AppendChild(contentPresenter);
            template.VisualTree = border;

            var mouseOverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            mouseOverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(hoverColor))));
            template.Triggers.Add(mouseOverTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            button.Style = style;

            return button;
        }

        private static void HandleDecimalOkClick(Window window, TextBox inputTextBox, Dictionary<string, Func<decimal, bool>> validationRules)
        {
            if (!decimal.TryParse(inputTextBox.Text, out decimal value))
            {
                ShowValidationError("Please enter a valid number.");
                return;
            }

            var validationErrors = new List<string>();

            foreach (var rule in validationRules)
            {
                if (!rule.Value(value))
                {
                    switch (rule.Key)
                    {
                        case "positive":
                            validationErrors.Add("Value must be greater than zero");
                            break;
                        case "range":
                            validationErrors.Add("Value must be between 0.01 and 999,999.99");
                            break;
                        case "precision":
                            validationErrors.Add("Value cannot have more than 2 decimal places");
                            break;
                    }
                }
            }

            if (validationErrors.Count > 0)
            {
                ShowValidationError(string.Join(Environment.NewLine, validationErrors));
                return;
            }

            window.Tag = value;
            window.DialogResult = true;
        }

        private static void HandlePasswordOkClick(Window window, PasswordBox passwordBox)
        {
            if (string.IsNullOrWhiteSpace(passwordBox.Password))
            {
                ShowValidationError("Password cannot be empty.");
                return;
            }

            if (passwordBox.Password.Length < 6)
            {
                ShowValidationError("Password must be at least 6 characters long.");
                return;
            }

            window.Tag = passwordBox.Password;
            window.DialogResult = true;
        }

        private static void ConfigureWindowEvents(Window window, TextBox inputTextBox)
        {
            window.Loaded += (s, e) =>
            {
                inputTextBox.Focus();
                inputTextBox.SelectAll();
            };

            window.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    var okButton = FindVisualChild<Button>(window, btn => btn.Content.ToString() == "OK");
                    okButton?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                else if (e.Key == Key.Escape)
                {
                    window.DialogResult = false;
                }
            };
        }

        private static bool IsValidDecimalInput(string input, string currentText, int selectionStart, int selectionLength)
        {
            if (string.IsNullOrEmpty(input))
                return true;

            var newText = currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, input);

            if (newText == "." || newText == ",")
                return true;

            return decimal.TryParse(newText, out _);
        }

        private static void ShowValidationError(string message)
        {
            MessageBox.Show(message, "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static T FindVisualChild<T>(DependencyObject parent, Func<T, bool> predicate = null) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T item && (predicate == null || predicate(item)))
                    return item;

                var childOfChild = FindVisualChild<T>(child, predicate);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        public static void ClearDialogHistory()
        {
            _dialogHistory.Clear();
        }

        public static DialogResult GetLastDialogResult()
        {
            return _dialogHistory.Count > 0 ? _dialogHistory.Peek() : new DialogResult();
        }
    }

    public struct DialogResult
    {
        public bool Success { get; set; }
        public decimal Value { get; set; }
        public string StringValue { get; set; }
    }
}
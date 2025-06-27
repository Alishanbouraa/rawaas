using System.Windows;

namespace QuickTechSystems.WPF.Views
{
    public partial class SalaryManagementWindow : Window
    {
        public SalaryManagementWindow()
        {
            InitializeComponent();
            SetupWindow();
        }

        private void SetupWindow()
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            if (screenWidth >= 1920)
            {
                Width = 1000;
                Height = 700;
            }
            else if (screenWidth >= 1366)
            {
                Width = 900;
                Height = 600;
            }
            else
            {
                Width = 800;
                Height = 550;
            }

            if (ActualHeight > screenHeight * 0.9)
            {
                Height = screenHeight * 0.9;
            }

            Left = (screenWidth - Width) / 2;
            Top = (screenHeight - Height) / 2;
        }

        protected override void OnSourceInitialized(System.EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.Topmost = false;
            this.Focus();
        }
    }
}
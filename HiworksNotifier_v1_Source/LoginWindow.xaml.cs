using System;
using System.Windows;

namespace HiworksNotifier
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailBox.Text;
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter both email and password.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Save or Clear Config based on Checkbox
            bool isAuto = AutoLoginCheck.IsChecked == true;
            if (isAuto)
            {
                ConfigManager.Save(email, password, true);
            }
            else
            {
                ConfigManager.Clear();
            }

            // Call App start logic
            (Application.Current as App)?.StartService(email, password);
            this.Close();
        }

        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
             (Application.Current as App)?.ExitApplication();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot open link: {ex.Message}");
            }
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}

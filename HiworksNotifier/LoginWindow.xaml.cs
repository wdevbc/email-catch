using System;
using System.Windows;

namespace HiworksNotifier
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            
            // Load Last Checkbox State
            var config = ConfigManager.Load();
            if (config != null)
            {
                AutoLoginCheck.IsChecked = config.LastCheckboxState;
            }
        }

        private async void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailBox.Text;
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("이메일과 비밀번호를 모두 입력해주세요.", "로그인 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Basic format check
            if (!email.Contains("@") || !email.Contains("."))
            {
                MessageBox.Show("올바른 이메일 형식이 아닙니다.", "로그인 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Verify Credentials
            var btn = sender as System.Windows.Controls.Button;
            if (btn != null) 
            {
                btn.IsEnabled = false;
                btn.Content = "확인 중...";
            }

            bool isValid = await System.Threading.Tasks.Task.Run(() => HiworksWatcher.VerifyCredentialsAsync(email, password));

            if (!isValid)
            {
                MessageBox.Show("로그인에 실패했습니다.\n아이디와 비밀번호를 확인해주세요.", "로그인 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                if (btn != null)
                {
                    btn.IsEnabled = true;
                    btn.Content = "로그인";
                }
                return;
            }

            // Save Config (Always save to persist checkbox state)
            bool isAuto = AutoLoginCheck.IsChecked == true;
            ConfigManager.Save(email, password, isAuto);

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

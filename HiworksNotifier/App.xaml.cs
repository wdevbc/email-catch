using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows;
using MimeKit;
using System.Windows.Forms; // Required for NotifyIcon
using Application = System.Windows.Application; // Resolve ambiguity
using MessageBox = System.Windows.MessageBox;

namespace HiworksNotifier
{
    public partial class App : Application
    {
        private static Mutex? _mutex;
        private HiworksWatcher? _watcher;
        private HashSet<string> _notifiedSignatures = new HashSet<string>();
        
        // System Tray Icon
        private NotifyIcon? _trayIcon;

        // Keep track of open notes to close them on logout
        private List<MainWindow> _openNotes = new List<MainWindow>();

        // New Features
        private bool _dndEnabled = false;
        private readonly List<MailNotification> _history = new List<MailNotification>();
        private ToolStripMenuItem? _historyMenu;
        private ToolStripMenuItem? _dndMenu;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. Register CodePages
            Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            // 2. Kill Zombies
            CleanupZombies();

            // 3. Mutex Check
            const string appName = "HiworksNotifier_SingleInstance_Mutex";
            bool createdNew;
            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("App is already running.", "Hiworks Notifier", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // 4. Initialize System Tray
            InitTrayIcon();

            // 5. Check Auto Login
            CheckAutoLogin();
        }

        private async void CheckAutoLogin()
        {
            var config = ConfigManager.Load();
            if (config != null && config.IsAutoLogin)
            {
                // Check 10 days expiration
                if ((DateTime.Now - config.LastAccess).TotalDays > 10)
                {
                    Console.WriteLine("[App] Auto Login expired (> 10 days).");
                    ConfigManager.Clear();
                    ShowLoginWindow();
                    MessageBox.Show("자동 로그인이 만료되었습니다. 다시 로그인해주세요.", "하이웍스 알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                var (email, pass) = ConfigManager.GetCredentials(config);
                if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(pass))
                {
                    Console.WriteLine($"[App] Auto Login found for {email}, verifying...");
                    
                    // Verify credentials first (especially important if offline on boot)
                    bool isValid = await System.Threading.Tasks.Task.Run(() => HiworksWatcher.VerifyCredentialsAsync(email, pass));
                    
                    if (isValid)
                    {
                        ConfigManager.UpdateAccessTime();
                        StartService(email, pass);
                    }
                    else
                    {
                        // Verification failed (maybe offline or changed password)
                        // Don't clear config yet, just show login window.
                        ShowLoginWindow();
                    }
                }
                else
                {
                    ShowLoginWindow();
                }
            }
            else
            {
                ShowLoginWindow();
            }
        }

        private void CleanupZombies()
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);
            foreach (var p in processes)
            {
                if (p.Id != currentProcess.Id)
                {
                    try { p.Kill(); p.WaitForExit(1000); } catch { }
                }
            }
        }

        private void InitTrayIcon()
        {
            try
            {
                // For Single-File App, Assembly.Location is empty. Use Process Module FileName.
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                
                if (string.IsNullOrEmpty(exePath))
                {
                    // Fallback (though unlikely to work if MainModule failed)
                    exePath = Environment.ProcessPath;
                }

                if (!string.IsNullOrEmpty(exePath))
                {
                    _trayIcon = new NotifyIcon
                    {
                        Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath),
                        Visible = true,
                        Text = "하이웍스 알림"
                    };

                    var contextMenu = new ContextMenuStrip();
                    
                    // Auto Startup Toggle
                var startupItem = new ToolStripMenuItem("윈도우 시작 시 실행");
                startupItem.Checked = AutoStartupManager.IsStartupEnabled();
                startupItem.Click += (s, e) =>
                {
                    bool newState = !startupItem.Checked;
                    AutoStartupManager.SetStartup(newState);
                    startupItem.Checked = newState;
                };
                contextMenu.Items.Add(startupItem);
                contextMenu.Items.Add("-");

                // Recent History
                _historyMenu = new ToolStripMenuItem("최근 알림");
                _historyMenu.Enabled = false; // Initially empty
                contextMenu.Items.Add(_historyMenu);

                // Do Not Disturb
                _dndMenu = new ToolStripMenuItem("방해 금지 모드");
                _dndMenu.CheckOnClick = true;
                _dndMenu.Click += (s, e) => { 
                    _dndEnabled = _dndMenu.Checked; 
                    if(_dndEnabled) _trayIcon.Text = "하이웍스 알림 (방해금지)";
                    else _trayIcon.Text = "하이웍스 알림";
                };
                contextMenu.Items.Add(_dndMenu);

                contextMenu.Items.Add("-");

                // Close All Notes
                contextMenu.Items.Add("모든 알림 닫기", null, (s, e) => CloseAllNotes());
                contextMenu.Items.Add("-");

                contextMenu.Items.Add("로그아웃", null, (s, e) => Logout());
                contextMenu.Items.Add("-");
                contextMenu.Items.Add("종료", null, (s, e) => ExitApplication());
                    
                    _trayIcon.ContextMenuStrip = contextMenu;
                    _trayIcon.DoubleClick += (s, e) => ShowLoginOrStatus();
                }
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"[App] Failed to init tray icon: {ex.Message}");
            }
        }

        private void ShowLoginOrStatus()
        {
            if (_watcher == null)
            {
                // Not logged in
                ShowLoginWindow();
            }
            else
            {
                 // Logged in. Maybe show a status window? Or assume user wants to bring notes to front?
                 // For now, just a tooltip or simple message
                 _trayIcon?.ShowBalloonTip(1000, "하이웍스 알림", "실행 중...", ToolTipIcon.Info);
            }
        }

        public void ShowLoginWindow()
        {
            // Ensure only one login window
            foreach (Window win in Application.Current.Windows)
            {
                if (win is LoginWindow)
                {
                    win.Activate();
                    return;
                }
            }
            
            var loginWin = new LoginWindow();
            loginWin.Show();
        }

        public void StartService(string id, string pw)
        {
            if (_watcher != null) StopService();

            try 
            {
                _watcher = new HiworksWatcher(id, pw);
                _watcher.NewMailReceived += OnNewMailReceived;
                _watcher.ConnectionLost += OnConnectionLost;
                _watcher.Start();
                
                _trayIcon?.ShowBalloonTip(3000, "하이웍스 알림", "메일 알림모드가 시작되었습니다.", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start watcher: {ex.Message}");
                ShowLoginWindow();
            }
        }

        public void CloseAllNotes()
        {
            var notes = new List<MainWindow>(_openNotes);
            foreach (var note in notes)
            {
                note.Close();
            }
            _openNotes.Clear();
        }

        public void Logout()
        {
            StopService();
            ConfigManager.Clear(); // Clear saved login
            
            // Close all notes
            // Create a copy to iterate safely
            var notes = new List<MainWindow>(_openNotes);
            foreach (var note in notes)
            {
                note.Close();
            }
            _openNotes.Clear();
            _notifiedSignatures.Clear(); // Clear history on logout? User preference. I'll clear it.

             ShowLoginWindow();
        }

        private void StopService()
        {
            if (_watcher != null)
            {
                _watcher.Stop();
                _watcher.NewMailReceived -= OnNewMailReceived;
                _watcher = null;
            }
        }

        public void ExitApplication()
        {
            _trayIcon?.Dispose();
            Shutdown();
        }

        private void OnNewMailReceived(object? sender, MimeMessage e)
        {
            Dispatcher.Invoke(() =>
            {
                var signature = $"{e.From}|{e.Subject}|{e.Date.ToString("yyyyMMddHHmm")}";

                if (_notifiedSignatures.Contains(signature)) return;
                
                // Optimization: Keep only last 100 signatures to save memory
                if (_notifiedSignatures.Count >= 100)
                {
                    // Remove the oldest one (first item)
                    _notifiedSignatures.Remove(_notifiedSignatures.First());
                }

                _notifiedSignatures.Add(signature);

                var notification = new MailNotification(
                    e.From.ToString(),
                    e.Subject,
                    e.Date.DateTime,
                    null
                );

                // 1. Add to History
                AddToHistory(notification);

                // 2. Check Do Not Disturb
                if (_dndEnabled)
                {
                    Console.WriteLine("[App] DND is on. Skipping popup.");
                    return;
                }

                var noteWindow = new MainWindow(notification);
                
                // Track open notes
                noteWindow.Closed += (s, args) => _openNotes.Remove(noteWindow);
                _openNotes.Add(noteWindow);

                // Clamp to WorkArea (Primary Screen) to avoid going under taskbar
                var workArea = SystemParameters.WorkArea;
                
                // Calculate position
                noteWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                
                double newLeft = workArea.Left + (workArea.Width - noteWindow.Width) / 2 + (_openNotes.Count * 20);
                double newTop = workArea.Top + (workArea.Height - noteWindow.Height) / 2 + (_openNotes.Count * 20);
                
                // Ensure it doesn't go off the bottom
                // Note: ActualHeight might be 0 until loaded, checking approximate or relying on re-layout.
                // Assuming Height ~100-150.
                if (newTop + 150 > workArea.Bottom)
                {
                    // Reset stack to top if it exceeds bottom
                    newTop = workArea.Top + 20;
                    // Could also reset Left to avoid endless diagonal
                    // newLeft = ...
                }
                
                noteWindow.Left = newLeft;
                noteWindow.Top = newTop;

                noteWindow.Show();
                noteWindow.Activate();

                // Screen Flash Effect (Per Monitor with DPI Correction)
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    var flash = new FlashWindow();
                    flash.WindowStartupLocation = WindowStartupLocation.Manual;
                    
                    // 1. Initial placement acting as "best guess" to spawn on the correct screen
                    // WinForms uses physical pixels. WPF uses logical.
                    // We set it blindly first, then correct it.
                    flash.Left = screen.Bounds.Left;
                    flash.Top = screen.Bounds.Top;
                    
                    // Show() is required to get PresentationSource
                    flash.Show();

                    // 2. Correct Size/Position based on that window's DPI
                    var source = PresentationSource.FromVisual(flash);
                    if (source?.CompositionTarget != null)
                    {
                        var m = source.CompositionTarget.TransformToDevice;
                        double dpiX = m.M11;
                        double dpiY = m.M22;

                        flash.Left = screen.Bounds.Left / dpiX;
                        flash.Top = screen.Bounds.Top / dpiY;
                        flash.Width = screen.Bounds.Width / dpiX;
                        flash.Height = screen.Bounds.Height / dpiY;
                    }
                    else
                    {
                        // Fallback
                        flash.Width = screen.Bounds.Width;
                        flash.Height = screen.Bounds.Height;
                    }
                }

                // Play Notification Sound
                System.Media.SystemSounds.Exclamation.Play();
            });
        }


        private void AddToHistory(MailNotification note)
        {
            _history.Insert(0, note);
            if (_history.Count > 10) _history.RemoveAt(_history.Count - 1);

            if (_historyMenu != null)
            {
                _historyMenu.DropDownItems.Clear();
                _historyMenu.Enabled = true;

                foreach (var item in _history)
                {
                    // Format: [14:30] Subject...
                    string label = $"[{item.Date.ToString("HH:mm")}] {item.Subject}";
                    if (label.Length > 30) label = label.Substring(0, 27) + "...";

                    var menuItem = new ToolStripMenuItem(label);
                    menuItem.Click += (s, e) => ReShowNotification(item);
                    _historyMenu.DropDownItems.Add(menuItem);
                }
            }
        }

        private void ReShowNotification(MailNotification note)
        {
            var noteWindow = new MainWindow(note);
             noteWindow.Closed += (s, args) => _openNotes.Remove(noteWindow);
            _openNotes.Add(noteWindow);
            noteWindow.Show();
            noteWindow.Activate();
        }

        private void OnConnectionLost()
        {
            Dispatcher.Invoke(() =>
            {
                Logout();
                MessageBox.Show("연결실패하여 자동으로 로그아웃됩니다. 아이디 비밀번호를 확인해주세요.", "하이웍스 알림", MessageBoxButton.OK, MessageBoxImage.Error);
                ShowLoginWindow();
            });
        }
    }
}

using System;
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

        private void CheckAutoLogin()
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
                    MessageBox.Show("Auto login expired. Please login again.", "Hiworks Notifier", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Valid
                    var (email, pass) = ConfigManager.GetCredentials(config);
                    if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(pass))
                    {
                        Console.WriteLine($"[App] Auto Login found for {email}");
                        ConfigManager.UpdateAccessTime();
                        StartService(email, pass);
                    }
                    else
                    {
                        ShowLoginWindow();
                    }
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
                        Text = "Hiworks Notifier"
                    };

                    var contextMenu = new ContextMenuStrip();
                    
                    // Auto Startup Toggle
                    var startupItem = new ToolStripMenuItem("Start with Windows");
                    startupItem.Checked = AutoStartupManager.IsStartupEnabled();
                    startupItem.Click += (s, e) =>
                    {
                        bool newState = !startupItem.Checked;
                        AutoStartupManager.SetStartup(newState);
                        startupItem.Checked = newState;
                    };
                    contextMenu.Items.Add(startupItem);
                    contextMenu.Items.Add("-");

                    contextMenu.Items.Add("Logout", null, (s, e) => Logout());
                    contextMenu.Items.Add("-");
                    contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());
                    
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
                 _trayIcon?.ShowBalloonTip(1000, "Hiworks Notifier", "Running...", ToolTipIcon.Info);
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
                _watcher.Start();
                
                _trayIcon?.ShowBalloonTip(3000, "Hiworks Notifier", $"Logged in as {id}", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start watcher: {ex.Message}");
                ShowLoginWindow();
            }
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
                
                _notifiedSignatures.Add(signature);

                var notification = new MailNotification(
                    e.From.ToString(),
                    e.Subject,
                    e.Date.DateTime,
                    null
                );

                var noteWindow = new MainWindow(notification);
                
                // Track open notes
                noteWindow.Closed += (s, args) => _openNotes.Remove(noteWindow);
                _openNotes.Add(noteWindow);

                noteWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                noteWindow.Left += (_openNotes.Count * 20); // Cascade slightly
                noteWindow.Top += (_openNotes.Count * 20);

                noteWindow.Show();
                noteWindow.Activate();
            });
        }
    }
}

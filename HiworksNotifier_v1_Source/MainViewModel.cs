using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MimeKit;

namespace HiworksNotifier
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private HiworksWatcher _watcher;
        
        // Strict HashSet to track (Sender + Subject + Date) combination
        private HashSet<string> _notifiedSignatures = new HashSet<string>();

        public ObservableCollection<MailNotification> Notifications { get; set; } = new ObservableCollection<MailNotification>();

        public MainViewModel()
        {
            // Initialize Watcher
            // TODO: Ensure credentials are correct
            _watcher = new HiworksWatcher("", "");
            _watcher.NewMailReceived += OnNewMailReceived;
            _watcher.Start();
        }

        private void OnNewMailReceived(object? sender, MimeMessage e)
        {
            // Marshal to UI Thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Create unique signature: Sender|Subject|Date(minute precision)
                // This prevents duplicates if server sends same mail again within same minute
                var signature = $"{e.From}|{e.Subject}|{e.Date.ToString("yyyyMMddHHmm")}";

                if (_notifiedSignatures.Contains(signature))
                {
                    Console.WriteLine($"[ViewModel] Duplicate rejected by Signature: {signature}");
                    return;
                }
                
                // Double check against current list (Visual Dedupe)
                if (Notifications.Any(n => n.RawSubject == e.Subject && Math.Abs((n.Date - e.Date.DateTime).TotalSeconds) < 60))
                {
                     Console.WriteLine($"[ViewModel] Duplicate rejected by Visual Check: {e.Subject}");
                     return;
                }

                _notifiedSignatures.Add(signature);

                Console.WriteLine($"[ViewModel] Adding New Notification: {e.Subject}");
                var notification = new MailNotification(
                    e.From.ToString(),
                    e.Subject,
                    e.Date.DateTime,
                    RemoveNotification
                );

                Notifications.Insert(0, notification);
                UpdateWindowVisibility();
            });
        }

        private void RemoveNotification(MailNotification notification)
        {
            if (Notifications.Contains(notification))
            {
                Notifications.Remove(notification);
                Console.WriteLine($"[ViewModel] Removed: {notification.Subject}");
            }
            UpdateWindowVisibility();
        }

        private void UpdateWindowVisibility()
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null) return;

            if (Notifications.Count > 0)
            {
                if (mainWindow.Visibility != Visibility.Visible)
                {
                    mainWindow.Show();
                    mainWindow.Activate();
                    mainWindow.Topmost = true; // Ensure top
                }
            }
            else
            {
                mainWindow.Hide();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

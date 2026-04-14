using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace HiworksNotifier
{
    public class HiworksWatcher
    {
        // ==========================================
        // USER CONFIGURATION (EDIT HERE)
        // ==========================================
        // Hiworks only supports POP3
        private const string Host = "pop3s.hiworks.com"; 
        private const int Port = 995;
        private const bool UseSsl = true;

        // TODO: Replace with your actual credentials
        private string _username = ""; 
        private string _password = "";
        // ==========================================

        private readonly System.Timers.Timer _timer;
        private bool _isChecking;
        
        // Cache for known UIDs to detect new messages
        private readonly HashSet<string> _knownUids = new HashSet<string>();
        private bool _isFirstCheck = true;
        
        public event EventHandler<MimeMessage>? NewMailReceived;

        public HiworksWatcher(string username, string password)
        {
            if (!string.IsNullOrEmpty(username)) _username = username;
            if (!string.IsNullOrEmpty(password)) _password = password;

            _timer = new System.Timers.Timer(10000); // Check every 10 seconds
            _timer.Elapsed += async (s, e) => await CheckMailWithTrackingAsync();
        }

        public void Start()
        {
            _timer.Start();
            Task.Run(() => CheckMailWithTrackingAsync());
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public async Task CheckMailWithTrackingAsync()
        {
             if (_isChecking) return;
            _isChecking = true;

            try
            {
                Console.WriteLine($"[{DateTime.Now}] Checking (POP3) for {_username}...");
                using (var client = new MailKit.Net.Pop3.Pop3Client())
                {
                    // Accept all certs
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    await client.ConnectAsync(Host, Port, UseSsl);
                    Console.WriteLine("Connected to POP3.");
                    
                    await client.AuthenticateAsync(_username, _password);
                    Console.WriteLine("Authenticated.");

                    // Get list of all UIDs
                    var uids = await client.GetMessageUidsAsync();
                    Console.WriteLine($"Total messages on server: {uids.Count}");

                    if (_isFirstCheck)
                    {
                        // First run: just add all to cache
                        foreach (var uid in uids)
                        {
                            _knownUids.Add(uid);
                        }
                        _isFirstCheck = false;
                        Console.WriteLine("Initial UID snapshot taken. No notifications this time.");
                    }
                    else
                    {
                        // Check for new UIDs
                        // POP3 list index corresponds to the UID list order
                        for (int i = 0; i < uids.Count; i++)
                        {
                            var uid = uids[i];
                            if (!_knownUids.Contains(uid))
                            {
                                Console.WriteLine($"New message detection: {uid}");
                                
                                // Fetch message (Index is i)
                                var message = await client.GetMessageAsync(i);
                                NewMailReceived?.Invoke(this, message);
                                
                                _knownUids.Add(uid);
                            }
                        }
                    }

                    await client.DisconnectAsync(true);
                }
            }
             catch (Exception ex)
            {
                Console.WriteLine($"Error checking mail: {ex.Message}");
            }
            finally
            {
                _isChecking = false;
            }
        }
    }
}

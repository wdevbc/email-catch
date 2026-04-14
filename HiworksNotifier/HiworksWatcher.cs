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

        private int _failureCount;
        
        // Cache for known UIDs to detect new messages
        private readonly HashSet<string> _knownUids = new HashSet<string>();
        private bool _isFirstCheck = true;
        
        public event EventHandler<MimeMessage>? NewMailReceived;
        public event Action? ConnectionLost;

        private CancellationTokenSource? _cancellationTokenSource;

        public HiworksWatcher(string username, string password)
        {
            if (!string.IsNullOrEmpty(username)) _username = username;
            if (!string.IsNullOrEmpty(password)) _password = password;
        }

        public void Start()
        {
            _failureCount = 0;
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Fire and forget the background polling loop
            _ = PollingLoopAsync(_cancellationTokenSource.Token);
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
        }

        public static async Task<bool> VerifyCredentialsAsync(string username, string password)
        {
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15))) // 15s timeout for login
                using (var client = new MailKit.Net.Pop3.Pop3Client())
                {
                    client.Timeout = 10000; // 10s socket timeout
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    await client.ConnectAsync(Host, Port, UseSsl, cts.Token);
                    await client.AuthenticateAsync(username, password, cts.Token);
                    await client.DisconnectAsync(true, cts.Token);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task PollingLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await CheckMailWithTrackingAsync(token);

                try
                {
                    // Wait 30 seconds before next check
                    await Task.Delay(TimeSpan.FromSeconds(30), token);
                }
                catch (TaskCanceledException)
                {
                    // Expected when Stop() is called
                    break;
                }
            }
        }

        public async Task CheckMailWithTrackingAsync(CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now}] Checking (POP3) for {_username}...");
                using (var client = new MailKit.Net.Pop3.Pop3Client())
                {
                    client.Timeout = 15000; // 15s absolute socket timeout
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    // Combine global cancellation with a tight 30s timeout per check
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    linkedCts.CancelAfter(TimeSpan.FromSeconds(30)); 

                    await client.ConnectAsync(Host, Port, UseSsl, linkedCts.Token);
                    await client.AuthenticateAsync(_username, _password, linkedCts.Token);

                    // Connection successful, reset failure count
                    _failureCount = 0;

                    var uids = await client.GetMessageUidsAsync(linkedCts.Token);

                    if (_isFirstCheck)
                    {
                        foreach (var uid in uids)
                        {
                            _knownUids.Add(uid);
                        }
                        _isFirstCheck = false;
                        Console.WriteLine("Initial UID snapshot taken. No notifications this time.");
                    }
                    else
                    {
                        for (int i = 0; i < uids.Count; i++)
                        {
                            var uid = uids[i];
                            if (!_knownUids.Contains(uid))
                            {
                                Console.WriteLine($"New message detection: {uid}");
                                var message = await client.GetMessageAsync(i, linkedCts.Token);
                                NewMailReceived?.Invoke(this, message);
                                _knownUids.Add(uid);
                            }
                        }
                    }

                    await client.DisconnectAsync(true, linkedCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Mail check cancelled or timed out.");
                // We treat timeout as a failure mode
                if (!cancellationToken.IsCancellationRequested) 
                {
                     _failureCount++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking mail: {ex.Message}");
                _failureCount++;
                Console.WriteLine($"Failure Count: {_failureCount}");

                if (_failureCount >= 10)
                {
                    Console.WriteLine("Too many failures. Stopping service.");
                    Stop();
                    ConnectionLost?.Invoke();
                }
            }
        }
    }
}

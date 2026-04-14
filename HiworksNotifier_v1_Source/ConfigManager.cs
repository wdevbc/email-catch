using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HiworksNotifier
{
    public class AppConfig
    {
        public bool IsAutoLogin { get; set; }
        public DateTime LastAccess { get; set; }
        public string? EmailEncrypted { get; set; }
        public string? PasswordEncrypted { get; set; }
    }

    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HiworksNotifier", "config.json");

        public static void Save(string email, string password, bool isAutoLogin)
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var config = new AppConfig
                {
                    IsAutoLogin = isAutoLogin,
                    LastAccess = DateTime.Now,
                    EmailEncrypted = Encrypt(email),
                    PasswordEncrypted = Encrypt(password)
                };

                var json = JsonSerializer.Serialize(config);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Config] Save failed: {ex.Message}");
            }
        }

        public static AppConfig? Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return null;
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json);
            }
            catch
            {
                return null;
            }
        }

        public static void UpdateAccessTime()
        {
            var config = Load();
            if (config != null)
            {
                config.LastAccess = DateTime.Now;
                var json = JsonSerializer.Serialize(config);
                File.WriteAllText(ConfigPath, json);
            }
        }

        public static void Clear()
        {
            if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
        }

        public static (string? email, string? password) GetCredentials(AppConfig config)
        {
            try
            {
                if (string.IsNullOrEmpty(config.EmailEncrypted) || string.IsNullOrEmpty(config.PasswordEncrypted)) return (null, null);
                return (Decrypt(config.EmailEncrypted), Decrypt(config.PasswordEncrypted));
            }
            catch
            {
                return (null, null);
            }
        }

        // Helpers using DPAPI (Windows Only)
        private static string Encrypt(string plainText)
        {
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        private static string Decrypt(string cipherText)
        {
            var bytes = Convert.FromBase64String(cipherText);
            var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
    }
}

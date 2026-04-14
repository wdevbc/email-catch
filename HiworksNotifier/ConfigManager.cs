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
        public bool LastCheckboxState { get; set; } // Persist UI state
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
                    LastCheckboxState = isAutoLogin, // If we are saving credentials, checkbox was checked (or user passed logic)
                    LastAccess = DateTime.Now,
                    EmailEncrypted = isAutoLogin ? Encrypt(email) : null,
                    PasswordEncrypted = isAutoLogin ? Encrypt(password) : null
                };
                
                // If not auto login, we still might want to save the LastCheckboxState?
                // Actually LoginWindow logic: 
                // if checked -> Save(..., true) -> IsAutoLogin=true, LastCheckboxState=true
                // if unchecked -> Clear() -> ...
                // We need to change LoginWindow logic to call Save even if unchecked, or use a new method.
                // Let's assume Save is called with isAutoLogin reflecting the checkbox.
                
                // Correction: If isAutoLogin is false, we don't save credentials.
                if (!isAutoLogin) 
                {
                     config.EmailEncrypted = null;
                     config.PasswordEncrypted = null;
                }

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
            // Instead of deleting, we clear credentials but keep preference if possible
            try 
            {
                var oldConfig = Load();
                bool lastState = oldConfig?.LastCheckboxState ?? false;
                
                var config = new AppConfig
                {
                    IsAutoLogin = false,
                    LastCheckboxState = lastState, // Preserve
                    LastAccess = DateTime.Now,
                    EmailEncrypted = null,
                    PasswordEncrypted = null
                };
                
                var json = JsonSerializer.Serialize(config);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
            }
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

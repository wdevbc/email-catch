using System;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace HiworksNotifier
{
    public static class AutoStartupManager
    {
        private const string AppName = "HiworksNotifier";

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key == null) return false;
                    return key.GetValue(AppName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;

                    if (enable)
                    {
                        // Use Process.MainModule.FileName for single-file published apps
                        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                        if (string.IsNullOrEmpty(exePath)) exePath = Environment.ProcessPath;

                        if (!string.IsNullOrEmpty(exePath))
                        {
                            // Wrap path in quotes to handle spaces
                            key.SetValue(AppName, $"\"{exePath}\"");
                        }
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoStartup] Failed to set startup: {ex.Message}");
            }
        }
    }
}

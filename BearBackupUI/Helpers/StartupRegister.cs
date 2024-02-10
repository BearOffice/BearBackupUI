using Microsoft.Win32;
using System.Diagnostics;

namespace BearBackupUI.Helpers;

public static class StartupRegister
{
    private static readonly string _keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    public static void AddStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath, true);
            var name = AppDomain.CurrentDomain.FriendlyName;
            var path = Process.GetCurrentProcess().MainModule?.FileName ?? throw new Exception();
            key?.SetValue(name, path);
        }
        catch { }
    }

    public static void RemoveStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath, true);
            var name = AppDomain.CurrentDomain.FriendlyName;
            var hasKey = key?.GetValue(name, null) is not null;
            if (hasKey) key?.DeleteValue(name);
        }
        catch { }
    }
}

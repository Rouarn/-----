using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace OnmyojiTools.App
{
    /// <summary>HKCU Run 启动项；只写当前用户，不需要管理员权限。</summary>
    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "OnmyojiTools";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    string value = key.GetValue(ValueName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch { return false; }
        }

        public static bool SetEnabled(bool enabled, out string error)
        {
            error = null;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null)
                    {
                        error = "无法打开当前用户启动项注册表。";
                        return false;
                    }

                    if (enabled)
                    {
                        string executable = Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue(ValueName, "\"" + executable + "\"", RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}

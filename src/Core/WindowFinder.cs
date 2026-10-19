using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>阴阳师窗口识别：枚举顶层窗口，按标题与进程路径过滤出游戏主窗口</summary>
    internal static class WindowFinder
    {
        public static string GetProcessPath(IntPtr hwnd)
        {
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return "";
            try
            {
                StringBuilder sb = new StringBuilder(1024);
                uint size = 1024;
                Native.QueryFullProcessImageName(h, 0, sb, ref size);
                return sb.ToString();
            }
            finally
            {
                Native.CloseHandle(h);
            }
        }

        public static string GetTitle(IntPtr hwnd)
        {
            StringBuilder sb = new StringBuilder(512);
            Native.GetWindowText(hwnd, sb, 512);
            return sb.ToString();
        }

        public static bool IsYysWindow(IntPtr hwnd)
        {
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == (uint)Process.GetCurrentProcess().Id) return false;

            string title = GetTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title)) return false;

            string lowerTitle = title.ToLowerInvariant();
            if (lowerTitle.Contains("mpay") || lowerTitle.Contains("pay") || lowerTitle.Contains("tips") ||
                lowerTitle.Contains("form") || lowerTitle.Contains("login") || lowerTitle.Contains("dialog") ||
                lowerTitle.Contains("popup") || lowerTitle.Contains("notice") || lowerTitle.Contains("menu") ||
                lowerTitle.Contains("setting") || lowerTitle.Contains("dlg") || lowerTitle.Contains("wnd"))
                return false;

            string path = GetProcessPath(hwnd).ToLowerInvariant();
            if (!path.Contains("onmyoji") && !path.Contains("yys") && !path.Contains("阴阳师"))
                return false;

            if (title.Contains("阴阳师") && !title.Contains("排列") && !title.Contains("工具"))
                return true;
            return false;
        }

        public static List<YysWindow> EnumerateYys()
        {
            List<YysWindow> list = new List<YysWindow>();
            Native.EnumWindows(delegate(IntPtr hwnd, IntPtr lp)
            {
                if (Native.IsWindowVisible(hwnd) && IsYysWindow(hwnd))
                {
                    uint pid;
                    Native.GetWindowThreadProcessId(hwnd, out pid);
                    Native.RECT r;
                    Native.GetWindowRect(hwnd, out r);
                    list.Add(new YysWindow { Hwnd = hwnd, Pid = pid, Title = GetTitle(hwnd), Rect = r, Focused = (Native.GetForegroundWindow() == hwnd) });
                }
                return true;
            }, IntPtr.Zero);
            return list;
        }
    }
}

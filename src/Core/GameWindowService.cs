using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>枚举阴阳师主窗口；每个 PID 的路径只查询一次。</summary>
    internal sealed class GameWindowService : IGameWindowService
    {
        private readonly int _selfProcessId;

        public GameWindowService()
        {
            using (Process self = Process.GetCurrentProcess()) _selfProcessId = self.Id;
        }

        public IReadOnlyList<GameWindowInfo> GetGameWindows()
        {
            List<GameWindowInfo> windows = new List<GameWindowInfo>();
            Dictionary<int, string> pathCache = new Dictionary<int, string>();
            IntPtr foreground = NativeMethods.GetForegroundWindow();

            NativeMethods.EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
            {
                try
                {
                    if (!NativeMethods.IsWindowVisible(hwnd)) return true;

                    uint rawPid;
                    NativeMethods.GetWindowThreadProcessId(hwnd, out rawPid);
                    int processId = (int)rawPid;
                    if (processId <= 0 || processId == _selfProcessId) return true;

                    string title = ReadWindowText(hwnd);
                    if (!GameWindowMatcher.IsGameTitle(title)) return true;

                    string path;
                    if (!pathCache.TryGetValue(processId, out path))
                    {
                        path = ProcessPathReader.TryGetExecutablePath(processId);
                        pathCache[processId] = path;
                    }

                    if (!GameWindowMatcher.IsGameWindow(title, path, processId, _selfProcessId)) return true;

                    NativeMethods.RECT rect;
                    if (!NativeMethods.GetWindowRect(hwnd, out rect) || rect.IsEmpty) return true;

                    windows.Add(new GameWindowInfo
                    {
                        Handle = hwnd,
                        ProcessId = processId,
                        Title = title,
                        Bounds = rect,
                        IsForeground = hwnd == foreground
                    });
                }
                catch { }
                return true;
            }, IntPtr.Zero);

            windows.Sort(delegate(GameWindowInfo a, GameWindowInfo b) { return a.ProcessId.CompareTo(b.ProcessId); });
            return windows;
        }

        private static string ReadWindowText(IntPtr hwnd)
        {
            StringBuilder buffer = new StringBuilder(512);
            try
            {
                NativeMethods.GetWindowText(hwnd, buffer, buffer.Capacity);
                return buffer.ToString();
            }
            catch { return ""; }
        }
    }
}

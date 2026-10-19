using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>一键关闭：先优雅关闭（自动点击退出确认框），超时 3 秒后强制结束</summary>
    internal static class GameCloser
    {
        public static void CloseAll(Action<string> log)
        {
            HashSet<int> targets = GameProcesses.CollectIds();

            if (targets.Count == 0)
            {
                log("未发现正在运行的阴阳师进程（onmyoji / yys）。");
                return;
            }

            log("开始关闭 " + targets.Count + " 个阴阳师进程（先优雅关闭，超时 3 秒后强制结束）...");
            foreach (int pid in targets)
            {
                try
                {
                    Process p = Process.GetProcessById(pid);
                    bool closed = p.CloseMainWindow();
                    if (closed)
                    {
                        for (int round = 0; round < 3 && !p.HasExited; round++)
                        {
                            bool clicked = false;
                            for (int i = 0; i < 30 && !p.HasExited; i++)
                            {
                                if (TryConfirmExit((uint)pid)) { clicked = true; break; }
                                Thread.Sleep(100);
                            }
                            if (clicked && !p.HasExited)
                            {
                                if (p.WaitForExit(1500)) break;
                            }
                        }
                    }
                    if (p.WaitForExit(3000))
                    {
                        log(string.Format("[PID {0}] 已优雅关闭", pid));
                    }
                    else
                    {
                        if (!p.HasExited) p.Kill();
                        log(string.Format("[PID {0}] 超时未退出，已强制结束", pid));
                    }
                    p.Dispose();
                }
                catch (Exception ex)
                {
                    log(string.Format("[PID {0}] 关闭失败：{1}", pid, ex.Message));
                }
            }
            log("全部关闭流程结束。");
        }

        // 定位游戏弹出的「退出游戏」确认对话框并点击确定（发送 BM_CLICK + 真实鼠标点击双保险）
        private static bool TryConfirmExit(uint pid)
        {
            IntPtr dlg = IntPtr.Zero;
            Native.EnumWindows((h, l) =>
            {
                uint wpid;
                Native.GetWindowThreadProcessId(h, out wpid);
                if (wpid != pid) return true;
                var cls = new StringBuilder(64);
                Native.GetClassName(h, cls, 64);
                if (cls.ToString() == "#32770")
                {
                    var t = new StringBuilder(256);
                    Native.GetWindowText(h, t, 256);
                    if (t.ToString().Contains("退出游戏")) { dlg = h; return false; }
                }
                return true;
            }, IntPtr.Zero);

            if (dlg == IntPtr.Zero) return false;

            IntPtr okBtn = IntPtr.Zero;
            Native.EnumChildWindows(dlg, (ch, cl) =>
            {
                var c = new StringBuilder(64);
                Native.GetClassName(ch, c, 64);
                if (c.ToString() == "Button")
                {
                    var t = new StringBuilder(256);
                    Native.GetWindowText(ch, t, 256);
                    string bt = t.ToString();
                    if (bt == "确定" || bt == "確定") { okBtn = ch; return false; }
                }
                return true;
            }, IntPtr.Zero);

            if (okBtn == IntPtr.Zero) return false;
            Native.SendMessage(okBtn, Native.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Native.RECT r;
            if (Native.GetWindowRect(okBtn, out r))
            {
                int cx = (r.L + r.R) / 2;
                int cy = (r.T + r.B) / 2;
                Native.SetCursorPos(cx, cy);
                Thread.Sleep(50);
                Native.mouse_event(Native.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                Thread.Sleep(50);
                Native.mouse_event(Native.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            }
            return true;
        }
    }
}

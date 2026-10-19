using System;
using System.Collections.Generic;
using System.Threading;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>窗口排列：探测-定位两阶段，先探测客户端接受的尺寸，再按网格排布</summary>
    internal static class WindowArranger
    {
        private const int PROBE_STEP_MS = 400;

        public static void Arrange(int cols, Action<string> log)
        {
            List<YysWindow> wins = WindowFinder.EnumerateYys();
            if (wins.Count == 0)
            {
                log("未识别到任何阴阳师窗口，请先启动游戏后点「刷新」。");
                return;
            }
            log("开始排列 " + wins.Count + " 个窗口，每行 " + cols + " 个");

            Native.RECT wa = new Native.RECT();
            Native.SystemParametersInfo(Native.SPI_GETWORKAREA, 0, ref wa, 0);
            int workW = wa.R - wa.L;
            int workH = wa.B - wa.T;
            int rows = (wins.Count + cols - 1) / cols;
            int cellW = workW / cols;
            int targetW = cellW - 8;
            int startH = Math.Min(workH / rows, 1000);

            log("探测目标尺寸（客户端会强制修正窗口尺寸）...");
            int aw, ah;
            ProbeSize(wins[0].Hwnd, targetW, startH, out aw, out ah);
            log(string.Format("客户端接受尺寸：{0} x {1}", aw, ah));

            int gap = 8;
            for (int i = 0; i < wins.Count; i++)
            {
                YysWindow w = wins[i];
                Native.ShowWindow(w.Hwnd, Native.SW_RESTORE);
                Native.SetWindowPos(w.Hwnd, IntPtr.Zero, 0, 0, aw, ah, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                Thread.Sleep(300);

                Native.RECT rc;
                Native.GetWindowRect(w.Hwnd, out rc);
                int cw = rc.R - rc.L;
                int ch = rc.B - rc.T;

                int row = i / cols;
                int col = i % cols;
                int x = wa.L + col * cellW + (cellW - cw) / 2;
                int y = wa.T + row * (ch + gap);

                Native.SetWindowPos(w.Hwnd, IntPtr.Zero, x, y, cw, ch, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                log(string.Format("[{0}/{1}] {2} -> ({3}, {4}) {5}x{6}", i + 1, wins.Count, w.Title, x, y, cw, ch));
            }
            log("排列完成。");
        }

        private static void ProbeSize(IntPtr hwnd, int targetW, int startH, out int bestW, out int bestH)
        {
            int h = startH;
            Native.ShowWindow(hwnd, Native.SW_RESTORE);
            while (h >= 300)
            {
                Native.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, targetW, h, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                Thread.Sleep(PROBE_STEP_MS);
                Native.RECT r;
                Native.GetWindowRect(hwnd, out r);
                int w = r.R - r.L;
                if (w <= targetW + 4)
                {
                    bestW = w;
                    bestH = r.B - r.T;
                    return;
                }
                h -= 40;
            }
            Native.RECT r2;
            Native.GetWindowRect(hwnd, out r2);
            bestW = r2.R - r2.L;
            bestH = r2.B - r2.T;
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace OnmyojiTools.Core
{
    /// <summary>多开启动器：从游戏根目录启动 onmyoji.exe，按固定间隔补足目标实例数</summary>
    internal static class GameLauncher
    {
        private const int INSTANCE_INTERVAL_MS = 8000;

        /// <summary>当前运行中的游戏实例数（排除工具自身）</summary>
        public static int CountRunning()
        {
            return GameProcesses.CollectIds().Count;
        }

        /// <summary>补足目标开数（调用前需先校验游戏根目录有效性）</summary>
        public static void Launch(int target, string yysRoot, Action<string> log)
        {
            log(string.Format("本次目标开数 = {0}（读自输入框）", target));
            int running = CountRunning();
            int need = Math.Max(0, target - running);
            log(string.Format("当前已开 {0} 个实例，目标 {1} 开，需新开 {2} 个", running, target, need));
            if (need == 0)
            {
                log("已满足目标开数，无需再开。");
                return;
            }
            for (int i = 0; i < need; i++)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = Path.Combine(yysRoot, "bin", "onmyoji.exe"),
                        WorkingDirectory = yysRoot,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    log(string.Format("已启动第 {0}/{1} 个新实例...", i + 1, need));
                }
                catch (Exception ex)
                {
                    log("启动失败：" + ex.Message);
                    break;
                }
                if (i < need - 1) Thread.Sleep(INSTANCE_INTERVAL_MS);
            }
            Thread.Sleep(6000);
            int now = CountRunning();
            log("启动流程结束，当前 onmyoji 进程数：" + now);
            if (now < target) log("警告：部分实例可能未常驻（游戏启动耗时较长，可稍后点「刷新」确认）。");
        }
    }
}

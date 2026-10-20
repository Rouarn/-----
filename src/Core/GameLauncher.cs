using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OnmyojiTools.Diagnostics;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>补足目标开数；间隔启动，支持取消。</summary>
    internal sealed class GameLauncher : IGameLauncher
    {
        private const int StartIntervalMilliseconds = 8000;
        private const int SettleMilliseconds = 6000;
        private const int WindowWaitMilliseconds = 8000;

        private readonly IGameProcessService _processes;
        private readonly IGameWindowService _windows;

        public GameLauncher(IGameProcessService processes, IGameWindowService windows = null)
        {
            _processes = processes;
            _windows = windows;
        }

        public async Task<LaunchResult> LaunchAsync(int target, string gameRoot, AppLogger logger, CancellationToken cancellationToken)
        {
            LaunchResult result = new LaunchResult();
            target = InputValidation.Clamp(target, InputValidation.MinInstances, InputValidation.MaxInstances);
            result.Target = target;

            string executable;
            string error;
            if (!InputValidation.TryValidateGameRoot(gameRoot, out executable, out error))
            {
                result.Error = error;
                logger.Log("多开失败：" + error);
                return result;
            }

            result.RunningBefore = _processes.CountRunningGames();
            int need = Math.Max(0, target - result.RunningBefore);
            logger.Log(string.Format("目标 {0} 开，当前 {1} 个游戏进程，需补开 {2} 个。", target, result.RunningBefore, need));

            if (need == 0)
            {
                result.RunningAfter = result.RunningBefore;
                logger.Log("已满足目标开数，无需再开。");
                return result;
            }

            try
            {
                for (int i = 0; i < need; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DateTime startedAt = DateTime.UtcNow;
                    int startedPid = 0;

                    try
                    {
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = executable,
                            WorkingDirectory = gameRoot,
                            UseShellExecute = true
                        };
                        using (Process started = Process.Start(startInfo))
                        {
                            if (started != null) startedPid = started.Id;
                        }
                        result.Started++;
                        logger.Log(string.Format("已启动第 {0}/{1} 个新游戏进程。", i + 1, need));
                    }
                    catch (Exception ex)
                    {
                        result.Error = "启动失败：" + ex.Message;
                        logger.LogError("启动游戏进程失败", ex);
                        break;
                    }

                    if (_windows != null && startedPid > 0)
                    {
                        bool ready = await WaitForWindowAsync(startedPid, logger, cancellationToken).ConfigureAwait(false);
                        logger.Log(ready
                            ? string.Format("[PID {0}] 游戏窗口已就绪。", startedPid)
                            : string.Format("[PID {0}] 窗口未在 {1} 秒内出现，稍后可刷新确认。",
                                startedPid, WindowWaitMilliseconds / 1000));
                    }

                    if (i < need - 1)
                    {
                        int elapsed = (int)(DateTime.UtcNow - startedAt).TotalMilliseconds;
                        int remaining = Math.Max(0, StartIntervalMilliseconds - elapsed);
                        if (remaining > 0)
                            await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (string.IsNullOrEmpty(result.Error) && result.Started > 0)
                {
                    try { await Task.Delay(SettleMilliseconds, cancellationToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) { result.Canceled = true; }
                }
            }
            catch (OperationCanceledException)
            {
                result.Canceled = true;
                logger.Log("多开操作已取消。");
            }

            result.RunningAfter = _processes.CountRunningGames();
            logger.Log(string.Format("多开流程结束，当前游戏进程 {0} 个。", result.RunningAfter));
            if (result.RunningAfter < target && !result.Canceled && string.IsNullOrEmpty(result.Error))
                logger.Log("提示：部分游戏进程可能仍在启动中，稍后点顶部「刷新」确认。");
            return result;
        }

        private async Task<bool> WaitForWindowAsync(int processId, AppLogger logger, CancellationToken cancellationToken)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(WindowWaitMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var windows = _windows.GetGameWindows();
                    for (int i = 0; i < windows.Count; i++)
                    {
                        if (windows[i].ProcessId != processId) continue;
                        if (!NativeMethods.IsWindow(windows[i].Handle)) continue;

                        IntPtr result;
                        IntPtr ok = NativeMethods.SendMessageTimeout(windows[i].Handle, NativeMethods.WM_NULL,
                            IntPtr.Zero, IntPtr.Zero,
                            NativeMethods.SMTO_ABORTIFHUNG | NativeMethods.SMTO_BLOCK, 150, out result);
                        if (ok != IntPtr.Zero) return true;
                    }
                }
                catch (Exception ex)
                {
                    logger.Log("等待游戏窗口时读取失败：" + ex.Message);
                    return false;
                }
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
            return false;
        }
    }
}

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OnmyojiTools.Diagnostics;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>优雅关闭游戏窗口；支持全部关闭和单个实例关闭。</summary>
    internal sealed class GameShutdownService : IGameShutdownService
    {
        private const int DialogWaitMilliseconds = 4000;
        private const int GracefulWaitMilliseconds = 3000;
        private const int ForcedWaitMilliseconds = 2000;

        private readonly IGameProcessService _processes;

        public GameShutdownService(IGameProcessService processes)
        {
            _processes = processes;
        }

        public async Task<CloseResult> CloseAllAsync(AppLogger logger, CancellationToken cancellationToken)
        {
            CloseResult result = new CloseResult();
            try
            {
                var games = _processes.GetRunningGames();
                result.Total = games.Count;
                if (games.Count == 0)
                {
                    logger.Log("未发现正在运行的阴阳师进程。");
                    return result;
                }

                logger.Log(string.Format("开始关闭 {0} 个游戏进程...", games.Count));

                for (int i = 0; i < games.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (await CloseOneAsync(games[i], result, logger, cancellationToken).ConfigureAwait(false))
                        break;
                }

                logger.Log(string.Format("关闭流程结束：优雅 {0} 个，强制 {1} 个，失败 {2} 个。",
                    result.Graceful, result.Forced, result.Failed));
            }
            catch (OperationCanceledException)
            {
                result.Canceled = true;
                logger.Log("关闭操作已取消。");
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                logger.LogError("关闭流程失败", ex);
            }

            return result;
        }

        public async Task<CloseResult> CloseAsync(int processId, AppLogger logger, CancellationToken cancellationToken)
        {
            CloseResult result = new CloseResult { Total = 1 };
            try
            {
                GameProcessInfo found = null;
                var games = _processes.GetRunningGames();
                for (int i = 0; i < games.Count; i++)
                {
                    if (games[i].ProcessId != processId) continue;
                    found = games[i];
                    break;
                }

                if (found == null)
                {
                    result.Failed = 1;
                    logger.Log(string.Format("[PID {0}] 未找到正在运行的游戏实例。", processId));
                    return result;
                }

                logger.Log(string.Format("开始关闭单个实例 PID {0}...", processId));
                await CloseOneAsync(found, result, logger, cancellationToken).ConfigureAwait(false);
                logger.Log(string.Format("关闭实例 PID {0} 结束：优雅 {1}，强制 {2}，失败 {3}。",
                    processId, result.Graceful, result.Forced, result.Failed));
            }
            catch (OperationCanceledException)
            {
                result.Canceled = true;
                logger.Log("关闭实例已取消。");
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                logger.LogError("关闭实例失败", ex);
            }

            return result;
        }

        /// <summary>关闭一个实例；返回 true 表示操作已取消。</summary>
        private static async Task<bool> CloseOneAsync(GameProcessInfo info, CloseResult result,
            AppLogger logger, CancellationToken cancellationToken)
        {
            Process process = null;
            try
            {
                process = Process.GetProcessById(info.ProcessId);
                if (process.HasExited) return false;

                if (info.StartTime.HasValue)
                {
                    DateTime? currentStart = TryGetStartTime(process);
                    if (currentStart.HasValue && currentStart.Value != info.StartTime.Value)
                    {
                        logger.Log(string.Format("[PID {0}] 已被其他进程复用，跳过。", info.ProcessId));
                        return false;
                    }
                }

                bool requested = process.CloseMainWindow();
                if (!requested)
                    logger.Log(string.Format("[PID {0}] 未找到主窗口，继续等待其自行退出。", info.ProcessId));

                bool confirmClicked = false;
                DateTime deadline = DateTime.UtcNow.AddMilliseconds(DialogWaitMilliseconds);
                while (!process.HasExited && DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    IntPtr dialog = FindExitDialog(info.ProcessId);
                    if (dialog != IntPtr.Zero)
                    {
                        if (await TryConfirmExitAsync(dialog, cancellationToken).ConfigureAwait(false))
                        {
                            confirmClicked = true;
                            break;
                        }
                    }
                    await Task.Delay(150, cancellationToken).ConfigureAwait(false);
                }

                if (confirmClicked)
                    logger.Log(string.Format("[PID {0}] 已点击退出确认。", info.ProcessId));

                if (await WaitForExitAsync(process, GracefulWaitMilliseconds, cancellationToken).ConfigureAwait(false))
                {
                    result.Graceful++;
                    logger.Log(string.Format("[PID {0}] 已优雅关闭。", info.ProcessId));
                    return false;
                }

                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    logger.Log(string.Format("[PID {0}] 强制结束失败：{1}", info.ProcessId, ex.Message));
                    return false;
                }

                if (await WaitForExitAsync(process, ForcedWaitMilliseconds, cancellationToken).ConfigureAwait(false))
                {
                    result.Forced++;
                    logger.Log(string.Format("[PID {0}] 超时未退出，已强制结束。", info.ProcessId));
                }
                else
                {
                    result.Failed++;
                    logger.Log(string.Format("[PID {0}] 仍未能结束。", info.ProcessId));
                }
            }
            catch (OperationCanceledException)
            {
                result.Canceled = true;
                logger.Log("关闭操作已取消。");
                return true;
            }
            catch (Exception ex)
            {
                result.Failed++;
                logger.Log(string.Format("[PID {0}] 关闭失败：{1}", info.ProcessId, ex.Message));
            }
            finally
            {
                if (process != null) process.Dispose();
            }
            return false;
        }

        private static async Task<bool> WaitForExitAsync(Process process, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            int elapsed = 0;
            while (elapsed < timeoutMilliseconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { if (process.HasExited) return true; }
                catch { return true; }

                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                elapsed += 100;
            }

            try { return process.HasExited; }
            catch { return true; }
        }

        private static DateTime? TryGetStartTime(Process process)
        {
            try { return process.StartTime; }
            catch { return null; }
        }

        private static IntPtr FindExitDialog(int processId)
        {
            IntPtr found = IntPtr.Zero;
            NativeMethods.EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
            {
                try
                {
                    uint rawPid;
                    NativeMethods.GetWindowThreadProcessId(hwnd, out rawPid);
                    if ((int)rawPid != processId) return true;

                    StringBuilder className = new StringBuilder(64);
                    NativeMethods.GetClassName(hwnd, className, className.Capacity);
                    if (!string.Equals(className.ToString(), "#32770", StringComparison.Ordinal)) return true;

                    StringBuilder title = new StringBuilder(256);
                    NativeMethods.GetWindowText(hwnd, title, title.Capacity);
                    if (title.ToString().IndexOf("退出游戏", StringComparison.Ordinal) < 0) return true;

                    found = hwnd;
                    return false;
                }
                catch { return true; }
            }, IntPtr.Zero);
            return found;
        }

        private static IntPtr FindConfirmButton(IntPtr dialog)
        {
            IntPtr found = IntPtr.Zero;
            NativeMethods.EnumChildWindows(dialog, delegate(IntPtr child, IntPtr lParam)
            {
                try
                {
                    StringBuilder className = new StringBuilder(64);
                    NativeMethods.GetClassName(child, className, className.Capacity);
                    if (!string.Equals(className.ToString(), "Button", StringComparison.Ordinal)) return true;

                    StringBuilder text = new StringBuilder(128);
                    NativeMethods.GetWindowText(child, text, text.Capacity);
                    string label = text.ToString();
                    if (label == "确定" || label == "確定")
                    {
                        found = child;
                        return false;
                    }
                }
                catch { }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static async Task<bool> TryConfirmExitAsync(IntPtr dialog, CancellationToken cancellationToken)
        {
            IntPtr button = FindConfirmButton(dialog);
            if (button == IntPtr.Zero) return false;

            NativeMethods.SendMessage(button, NativeMethods.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            if (!NativeMethods.IsWindow(dialog)) return true;

            NativeMethods.RECT buttonRect;
            if (!NativeMethods.GetWindowRect(button, out buttonRect)) return false;
            if (buttonRect.Width <= 0 || buttonRect.Height <= 0) return false;

            NativeMethods.POINT original;
            bool haveOriginal = NativeMethods.GetCursorPos(out original);
            try
            {
                int x = buttonRect.Left + buttonRect.Width / 2;
                int y = buttonRect.Top + buttonRect.Height / 2;
                NativeMethods.SetCursorPos(x, y);
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                return !NativeMethods.IsWindow(dialog);
            }
            catch { return false; }
            finally
            {
                if (haveOriginal)
                {
                    try { NativeMethods.SetCursorPos(original.X, original.Y); }
                    catch { }
                }
            }
        }
    }
}

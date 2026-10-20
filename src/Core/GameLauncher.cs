using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OnmyojiTools.Diagnostics;

namespace OnmyojiTools.Core
{
    /// <summary>补足目标开数；间隔启动，支持取消。</summary>
    internal sealed class GameLauncher : IGameLauncher
    {
        private const int StartIntervalMilliseconds = 8000;
        private const int SettleMilliseconds = 6000;

        private readonly IGameProcessService _processes;

        public GameLauncher(IGameProcessService processes)
        {
            _processes = processes;
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
            logger.Log(string.Format("目标 {0} 开，当前 {1} 个，需补开 {2} 个。", target, result.RunningBefore, need));

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
                            // 只负责拉起，不等待游戏自身初始化
                        }
                        result.Started++;
                        logger.Log(string.Format("已启动第 {0}/{1} 个新实例。", i + 1, need));
                    }
                    catch (Exception ex)
                    {
                        result.Error = "启动失败：" + ex.Message;
                        logger.LogError("启动实例失败", ex);
                        break;
                    }

                    if (i < need - 1)
                        await Task.Delay(StartIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
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
            logger.Log(string.Format("多开流程结束，当前运行 {0} 个实例。", result.RunningAfter));
            if (result.RunningAfter < target && !result.Canceled && string.IsNullOrEmpty(result.Error))
                logger.Log("提示：部分实例可能仍在启动中，稍后可点刷新确认。");
            return result;
        }
    }
}

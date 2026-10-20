using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OnmyojiTools.Diagnostics;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>识别到的游戏进程。</summary>
    internal sealed class GameProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public string ExecutablePath { get; set; }
        public DateTime? StartTime { get; set; }
    }

    /// <summary>识别到的游戏主窗口。</summary>
    internal sealed class GameWindowInfo
    {
        public IntPtr Handle { get; set; }
        public int ProcessId { get; set; }
        public string Title { get; set; }
        public NativeMethods.RECT Bounds { get; set; }
        public bool IsForeground { get; set; }
    }

    /// <summary>所有后台操作结果的公共契约，控制器据此统一处理取消和异常。</summary>
    internal interface IOperationResult
    {
        bool Canceled { get; set; }
        string Error { get; set; }
    }

    internal sealed class LaunchResult : IOperationResult
    {
        public int Target { get; set; }
        public int RunningBefore { get; set; }
        public int Started { get; set; }
        public int RunningAfter { get; set; }
        public bool Canceled { get; set; }
        public string Error { get; set; }
        public bool Succeeded { get { return Error == null && !Canceled; } }
    }

    internal sealed class ArrangeResult : IOperationResult
    {
        public int WindowCount { get; set; }
        public int Columns { get; set; }
        public int ProbeWidth { get; set; }
        public int ProbeHeight { get; set; }
        public int Moved { get; set; }
        public int Failed { get; set; }
        public bool Canceled { get; set; }
        public string Error { get; set; }
    }

    internal sealed class CloseResult : IOperationResult
    {
        public int Total { get; set; }
        public int Graceful { get; set; }
        public int Forced { get; set; }
        public int Failed { get; set; }
        public bool Canceled { get; set; }
        public string Error { get; set; }
    }

    internal interface IGameProcessService
    {
        HashSet<int> GetRunningProcessIds();
        IReadOnlyList<GameProcessInfo> GetRunningGames();
        int CountRunningGames();
    }

    internal interface IGameWindowService
    {
        IReadOnlyList<GameWindowInfo> GetGameWindows();
    }

    internal interface IGameLauncher
    {
        Task<LaunchResult> LaunchAsync(int target, string gameRoot, AppLogger logger, CancellationToken cancellationToken);
    }

    internal interface IWindowArranger
    {
        Task<ArrangeResult> ArrangeAsync(int columns, AppLogger logger, CancellationToken cancellationToken);
    }

    internal interface IGameShutdownService
    {
        Task<CloseResult> CloseAllAsync(AppLogger logger, CancellationToken cancellationToken);
    }
}

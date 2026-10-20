using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OnmyojiTools.Core;
using OnmyojiTools.Diagnostics;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Audio
{
    /// <summary>
    /// 声音跟随聚焦：每 500ms 读取前台窗口 PID，把所有游戏实例设为 100% 或 15%。
    /// 停止监控时会把仍在运行的游戏音量恢复到 100%。
    /// </summary>
    internal sealed class SoundFocusService : IDisposable
    {
        private const int ScanIntervalMilliseconds = 500;
        private const float FocusVolume = 1.0f;
        private const float BackgroundVolume = 0.15f;

        private readonly IGameProcessService _processes;
        private readonly IAudioSessionService _audio;
        private readonly AppLogger _logger;
        private readonly object _gate = new object();

        private CancellationTokenSource _cancellation;
        private Task _loop;
        private bool _disposed;
        private int _lastFocusedProcessId = -1;
        private int _lastMissingSessions = -1;

        public SoundFocusService(IGameProcessService processes, IAudioSessionService audio, AppLogger logger)
        {
            _processes = processes;
            _audio = audio;
            _logger = logger;
        }

        public bool IsRunning
        {
            get { lock (_gate) return _cancellation != null; }
        }

        public void Start()
        {
            lock (_gate)
            {
                if (_disposed || _cancellation != null) return;
                _cancellation = new CancellationTokenSource();
                CancellationToken token = _cancellation.Token;
                _lastFocusedProcessId = -1;
                _lastMissingSessions = -1;
                _loop = Task.Run(delegate { Loop(token); });
            }
            _logger.Log("声音跟随聚焦监控已开启：聚焦窗口正常音量，失焦窗口自动调小。");
        }

        public void Stop()
        {
            CancellationTokenSource cancellation;
            Task loop;
            lock (_gate)
            {
                cancellation = _cancellation;
                loop = _loop;
                _cancellation = null;
                _loop = null;
            }
            if (cancellation == null) return;

            try { cancellation.Cancel(); }
            catch { }

            try { if (loop != null) loop.Wait(1500); }
            catch { }

            cancellation.Dispose();
            RestoreVolumes();
            _logger.Log("声音跟随聚焦已关闭，已恢复游戏音量。");
        }

        private void Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    ScanOnce(token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                        _logger.Log("声音检测失败：" + ex.Message);
                }

                if (token.WaitHandle.WaitOne(ScanIntervalMilliseconds)) return;
            }
        }

        private void ScanOnce(CancellationToken token)
        {
            HashSet<int> games = _processes.GetRunningProcessIds();
            if (games.Count == 0)
            {
                _lastFocusedProcessId = 0;
                _lastMissingSessions = -1;
                return;
            }

            int focusedProcessId = GetForegroundProcessId();
            if (focusedProcessId != _lastFocusedProcessId)
            {
                _lastFocusedProcessId = focusedProcessId;
                bool focusedGame = games.Contains(focusedProcessId);
                _logger.Log(focusedGame
                    ? "焦点切换到游戏窗口 (PID " + focusedProcessId + ")。"
                    : "当前没有游戏窗口获得焦点，所有实例已降音。");
            }

            Dictionary<int, float> desired = new Dictionary<int, float>(games.Count);
            foreach (int processId in games)
                desired[processId] = processId == focusedProcessId ? FocusVolume : BackgroundVolume;

            token.ThrowIfCancellationRequested();
            HashSet<int> applied = _audio.ApplyVolumes(desired);

            int missing = desired.Count - applied.Count;
            if (missing > 0 && missing != _lastMissingSessions)
                _logger.Log("有 " + missing + " 个实例暂未找到音频会话，可能游戏音频尚未初始化。");
            _lastMissingSessions = missing;
        }

        private void RestoreVolumes()
        {
            try
            {
                HashSet<int> games = _processes.GetRunningProcessIds();
                if (games.Count == 0) return;

                Dictionary<int, float> desired = new Dictionary<int, float>(games.Count);
                foreach (int processId in games) desired[processId] = 1.0f;
                _audio.ApplyVolumes(desired);
            }
            catch (Exception ex)
            {
                _logger.Log("恢复游戏音量失败：" + ex.Message);
            }
        }

        private static int GetForegroundProcessId()
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return 0;
            uint processId;
            NativeMethods.GetWindowThreadProcessId(foreground, out processId);
            return unchecked((int)processId);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
            }
            Stop();
        }
    }
}

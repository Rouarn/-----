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
    /// 唯一的前台检测来源：每 500ms 读取前台窗口 PID，并把焦点变化通过事件广播给界面与音量控制。
    /// 声音跟随关闭时仍继续检测（只发焦点事件，不碰音量），因此 04 列表状态始终跟随焦点。
    /// </summary>
    internal sealed class SoundFocusService : IDisposable
    {
        private const int ScanIntervalMilliseconds = 500;
        private const float FocusVolume = 1.0f;
        private const float DefaultBackgroundVolume = 0.15f;

        private readonly IGameProcessService _processes;
        private readonly IAudioSessionService _audio;
        private readonly AppLogger _logger;
        private readonly object _gate = new object();
        private readonly object _volumeGate = new object();
        private readonly Dictionary<int, float> _originalVolumes = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _manualVolumes = new Dictionary<int, float>();

        private CancellationTokenSource _cancellation;
        private Task _loop;
        private bool _disposed;
        private volatile bool _volumeControlEnabled = true;
        private float _backgroundVolume = DefaultBackgroundVolume;
        private int _lastFocusedProcessId = -1;
        private int _lastMissingSessions = -1;
        private int _foregroundProcessId;

        public SoundFocusService(IGameProcessService processes, IAudioSessionService audio, AppLogger logger,
            bool volumeControlEnabled = true, float backgroundVolume = DefaultBackgroundVolume)
        {
            _processes = processes;
            _audio = audio;
            _logger = logger;
            _volumeControlEnabled = volumeControlEnabled;
            _backgroundVolume = ClampVolume(backgroundVolume);
        }

        /// <summary>前台焦点变化（PID，0 表示没有前台窗口）；可能在后台线程触发。</summary>
        public event Action<int> ForegroundChanged;

        public int ForegroundProcessId { get { return _foregroundProcessId; } }

        public float BackgroundVolume
        {
            get { lock (_volumeGate) return _backgroundVolume; }
        }

        /// <summary>更新失焦音量；声音跟随开启时立即应用到当前实例。</summary>
        public void SetBackgroundVolume(float volume)
        {
            lock (_volumeGate)
            {
                _backgroundVolume = ClampVolume(volume);
                if (!_volumeControlEnabled) return;
                ApplyCurrentLocked();
            }
        }

        /// <summary>是否允许调整游戏音量；关闭后检测循环仍运行，但不再修改音频。</summary>
        public bool VolumeControlEnabled
        {
            get { return _volumeControlEnabled; }
            set
            {
                lock (_volumeGate)
                {
                    if (_volumeControlEnabled == value) return;
                    _volumeControlEnabled = value;
                    if (!value)
                    {
                        _manualVolumes.Clear();
                        RestoreVolumesLocked();
                    }
                }
            }
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

            _logger.Log(_volumeControlEnabled
                ? "声音跟随聚焦监控已开启：聚焦窗口正常音量，失焦窗口自动调至 " +
                    (int)Math.Round(_backgroundVolume * 100) + "%。"
                : "前台焦点检测已启动（声音跟随关闭，仅更新界面状态）。");
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

            lock (_volumeGate)
            {
                _volumeControlEnabled = false;
                _manualVolumes.Clear();
                RestoreVolumesLocked();
            }
            _logger.Log("前台焦点检测已停止，游戏音量已恢复。");
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
            int focusedProcessId = GetForegroundProcessId();
            _foregroundProcessId = focusedProcessId;

            bool focusChanged = focusedProcessId != _lastFocusedProcessId;
            if (focusChanged)
            {
                _lastFocusedProcessId = focusedProcessId;
                Action<int> handler = ForegroundChanged;
                if (handler != null)
                {
                    try { handler(focusedProcessId); }
                    catch { }
                }
            }

            if (!_volumeControlEnabled)
            {
                _lastMissingSessions = -1;
                return;
            }

            lock (_volumeGate)
            {
                if (!_volumeControlEnabled) return;

                HashSet<int> games = _processes.GetRunningProcessIds();
                if (games.Count == 0)
                {
                    _originalVolumes.Clear();
                    _manualVolumes.Clear();
                    _lastMissingSessions = -1;
                    return;
                }

                EnsureOriginalsLocked(games);

                if (focusChanged)
                {
                    bool focusedGame = games.Contains(focusedProcessId);
                    _logger.Log(focusedGame
                        ? "焦点切换到游戏窗口 (PID " + focusedProcessId + ")。"
                        : "当前没有游戏窗口获得焦点，所有实例已降音。");
                }

                Dictionary<int, float> desired = BuildDesiredLocked(games);
                token.ThrowIfCancellationRequested();
                HashSet<int> applied = _audio.ApplyVolumes(desired);

                int missing = desired.Count - applied.Count;
                if (missing > 0 && missing != _lastMissingSessions)
                    _logger.Log("有 " + missing + " 个实例暂未找到音频会话，可能游戏音频尚未初始化。");
                _lastMissingSessions = missing;

                PruneMissing(games);
            }
        }

        /// <summary>手动设置某个实例的音量（0 即静音）；会覆盖该实例的聚焦跟随。</summary>
        public void SetInstanceVolume(int processId, float volume)
        {
            if (processId <= 0) return;
            float level = ClampVolume(volume);
            lock (_volumeGate)
            {
                EnsureOriginalLocked(processId);
                _manualVolumes[processId] = level;
                Dictionary<int, float> one = new Dictionary<int, float>();
                one[processId] = level;
                HashSet<int> applied = _audio.ApplyVolumes(one);
                _logger.Log("实例 PID " + processId + " 音量设为 " + (int)Math.Round(level * 100) + "%" +
                    (applied.Count == 0 ? "（暂未找到音频会话）" : "（手动覆盖）") + "。");
            }
        }

        /// <summary>清除手动音量，恢复聚焦跟随；跟随关闭时恢复该实例的原始音量。</summary>
        public void ClearInstanceVolume(int processId)
        {
            if (processId <= 0) return;
            lock (_volumeGate)
            {
                if (!_manualVolumes.Remove(processId)) return;

                HashSet<int> games = _processes.GetRunningProcessIds();
                if (!games.Contains(processId)) return;

                float level;
                if (_volumeControlEnabled)
                    level = processId == _foregroundProcessId ? FocusVolume : _backgroundVolume;
                else
                    level = GetOriginalOrOne(processId);

                Dictionary<int, float> one = new Dictionary<int, float>();
                one[processId] = level;
                _audio.ApplyVolumes(one);
                _logger.Log("实例 PID " + processId + " 已恢复" +
                    (_volumeControlEnabled ? "聚焦跟随。" : "原始音量。"));
            }
        }

        private void ApplyCurrentLocked()
        {
            HashSet<int> games = _processes.GetRunningProcessIds();
            if (games.Count == 0) return;
            EnsureOriginalsLocked(games);
            _audio.ApplyVolumes(BuildDesiredLocked(games));
            PruneMissing(games);
        }

        private Dictionary<int, float> BuildDesiredLocked(HashSet<int> games)
        {
            Dictionary<int, float> desired = new Dictionary<int, float>(games.Count);
            foreach (int processId in games)
            {
                float manual;
                if (_manualVolumes.TryGetValue(processId, out manual))
                    desired[processId] = manual;
                else
                    desired[processId] = processId == _foregroundProcessId ? FocusVolume : _backgroundVolume;
            }
            return desired;
        }

        private void EnsureOriginalsLocked(HashSet<int> games)
        {
            List<int> unknown = new List<int>();
            foreach (int processId in games)
            {
                if (!_originalVolumes.ContainsKey(processId)) unknown.Add(processId);
            }
            if (unknown.Count == 0) return;

            Dictionary<int, float> volumes = _audio.GetVolumes(unknown);
            for (int i = 0; i < unknown.Count; i++)
            {
                float level;
                if (volumes.TryGetValue(unknown[i], out level))
                    _originalVolumes[unknown[i]] = ClampVolume(level);
            }
        }

        private void EnsureOriginalLocked(int processId)
        {
            if (_originalVolumes.ContainsKey(processId)) return;
            List<int> one = new List<int>();
            one.Add(processId);
            Dictionary<int, float> volumes = _audio.GetVolumes(one);
            float level;
            if (volumes.TryGetValue(processId, out level))
                _originalVolumes[processId] = ClampVolume(level);
        }

        private float GetOriginalOrOne(int processId)
        {
            float level;
            return _originalVolumes.TryGetValue(processId, out level) ? level : 1.0f;
        }

        private void PruneMissing(HashSet<int> games)
        {
            List<int> stale = new List<int>();
            foreach (int processId in _originalVolumes.Keys)
            {
                if (!games.Contains(processId)) stale.Add(processId);
            }
            for (int i = 0; i < stale.Count; i++) _originalVolumes.Remove(stale[i]);

            stale.Clear();
            foreach (int processId in _manualVolumes.Keys)
            {
                if (!games.Contains(processId)) stale.Add(processId);
            }
            for (int i = 0; i < stale.Count; i++) _manualVolumes.Remove(stale[i]);
        }

        private static float ClampVolume(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private void RestoreVolumesLocked()
        {
            try
            {
                HashSet<int> games = _processes.GetRunningProcessIds();
                if (games.Count == 0) return;

                Dictionary<int, float> desired = new Dictionary<int, float>(games.Count);
                foreach (int processId in games)
                    desired[processId] = GetOriginalOrOne(processId);
                _manualVolumes.Clear();
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

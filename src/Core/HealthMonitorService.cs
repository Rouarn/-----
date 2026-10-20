using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OnmyojiTools.Diagnostics;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    internal sealed class InstanceHealth
    {
        public int ProcessId { get; set; }
        public bool Responsive { get; set; }
    }

    internal interface IHealthMonitor : IDisposable
    {
        event Action<IReadOnlyList<InstanceHealth>> HealthUpdated;
        void Start();
        void Stop();
    }

    /// <summary>
    /// 每 2 秒用 SendMessageTimeout(WM_NULL) 探测游戏窗口是否响应。
    /// 只探测当前可见游戏窗口；无响应不会自动重启，只上报界面与日志。
    /// </summary>
    internal sealed class HealthMonitorService : IHealthMonitor
    {
        private const int ScanIntervalMilliseconds = 2000;
        private const uint ProbeTimeoutMilliseconds = 150;

        private readonly IGameWindowService _windows;
        private readonly AppLogger _logger;
        private readonly object _gate = new object();
        private readonly Dictionary<int, bool> _lastStates = new Dictionary<int, bool>();
        private CancellationTokenSource _cancellation;
        private Task _loop;
        private bool _disposed;

        public HealthMonitorService(IGameWindowService windows, AppLogger logger)
        {
            _windows = windows;
            _logger = logger;
        }

        public event Action<IReadOnlyList<InstanceHealth>> HealthUpdated;

        public void Start()
        {
            lock (_gate)
            {
                if (_disposed || _cancellation != null) return;
                _cancellation = new CancellationTokenSource();
                CancellationToken token = _cancellation.Token;
                _loop = Task.Run(delegate { Loop(token); });
            }
            _logger.Log("实例健康监控已开启（每 2 秒检查一次响应）。");
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

            lock (_gate) _lastStates.Clear();
        }

        private void Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    ScanOnce();
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                        _logger.Log("健康检查失败：" + ex.Message);
                }

                if (token.WaitHandle.WaitOne(ScanIntervalMilliseconds)) return;
            }
        }

        private void ScanOnce()
        {
            List<InstanceHealth> health = new List<InstanceHealth>();
            HashSet<int> seen = new HashSet<int>();
            IReadOnlyList<GameWindowInfo> windows = _windows.GetGameWindows();

            for (int i = 0; i < windows.Count; i++)
            {
                int processId = windows[i].ProcessId;
                if (processId <= 0 || !seen.Add(processId)) continue;

                bool responsive = false;
                try
                {
                    if (NativeMethods.IsWindow(windows[i].Handle))
                    {
                        IntPtr result;
                        IntPtr ok = NativeMethods.SendMessageTimeout(windows[i].Handle, NativeMethods.WM_NULL,
                            IntPtr.Zero, IntPtr.Zero,
                            NativeMethods.SMTO_ABORTIFHUNG | NativeMethods.SMTO_BLOCK,
                            ProbeTimeoutMilliseconds, out result);
                        responsive = ok != IntPtr.Zero;
                    }
                }
                catch { responsive = false; }

                health.Add(new InstanceHealth { ProcessId = processId, Responsive = responsive });
            }

            lock (_gate)
            {
                for (int i = 0; i < health.Count; i++)
                {
                    int processId = health[i].ProcessId;
                    bool previous;
                    if (!_lastStates.TryGetValue(processId, out previous)) previous = true;
                    if (previous != health[i].Responsive)
                    {
                        if (health[i].Responsive)
                            _logger.Log("实例 PID " + processId + " 已恢复响应。");
                        else
                            _logger.Log("实例 PID " + processId + " 暂时无响应。");
                    }
                    _lastStates[processId] = health[i].Responsive;
                }

                List<int> gone = new List<int>();
                foreach (int processId in _lastStates.Keys)
                {
                    bool exists = false;
                    for (int i = 0; i < health.Count; i++)
                    {
                        if (health[i].ProcessId != processId) continue;
                        exists = true;
                        break;
                    }
                    if (!exists) gone.Add(processId);
                }
                for (int i = 0; i < gone.Count; i++) _lastStates.Remove(gone[i]);
            }

            Action<IReadOnlyList<InstanceHealth>> handler = HealthUpdated;
            if (handler != null)
            {
                try { handler(health); }
                catch { }
            }
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

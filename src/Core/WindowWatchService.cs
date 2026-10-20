using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OnmyojiTools.Diagnostics;

namespace OnmyojiTools.Core
{
    internal interface IWindowWatchService : IDisposable
    {
        event Action<IReadOnlyList<GameWindowInfo>, bool> WindowsChanged;
        void Start();
        void Stop();
    }

    /// <summary>每 2 秒比较窗口集合，只在新增/移除时通知；用于自动刷新与可选的新窗口自动排列。</summary>
    internal sealed class WindowWatchService : IWindowWatchService
    {
        private const int ScanIntervalMilliseconds = 2000;
        private readonly IGameWindowService _windows;
        private readonly AppLogger _logger;
        private readonly object _gate = new object();
        private readonly HashSet<string> _signature = new HashSet<string>();
        private CancellationTokenSource _cancellation;
        private Task _loop;
        private bool _disposed;
        private bool _hasSnapshot;

        public WindowWatchService(IGameWindowService windows, AppLogger logger)
        {
            _windows = windows;
            _logger = logger;
        }

        public event Action<IReadOnlyList<GameWindowInfo>, bool> WindowsChanged;

        public void Start()
        {
            lock (_gate)
            {
                if (_disposed || _cancellation != null) return;
                _cancellation = new CancellationTokenSource();
                CancellationToken token = _cancellation.Token;
                _loop = Task.Run(delegate { Loop(token); });
            }
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
            try { cancellation.Cancel(); } catch { }
            try { if (loop != null) loop.Wait(1500); } catch { }
            cancellation.Dispose();
        }

        private void Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try { ScanOnce(); }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                        _logger.Log("窗口变化检测失败：" + ex.Message);
                }
                if (token.WaitHandle.WaitOne(ScanIntervalMilliseconds)) return;
            }
        }

        private void ScanOnce()
        {
            IReadOnlyList<GameWindowInfo> windows = _windows.GetGameWindows();
            HashSet<string> next = new HashSet<string>();
            for (int i = 0; i < windows.Count; i++)
                next.Add(windows[i].ProcessId + ":" + windows[i].Handle.ToInt64());

            bool added = false;
            bool removed = false;
            lock (_gate)
            {
                foreach (string key in next)
                {
                    if (!_signature.Contains(key)) added = true;
                }
                foreach (string key in _signature)
                {
                    if (!next.Contains(key)) removed = true;
                }

                if (!_hasSnapshot)
                {
                    _hasSnapshot = true;
                    _signature.Clear();
                    foreach (string key in next) _signature.Add(key);
                    return;
                }

                if (!added && !removed) return;
                _signature.Clear();
                foreach (string key in next) _signature.Add(key);
            }

            Action<IReadOnlyList<GameWindowInfo>, bool> handler = WindowsChanged;
            if (handler != null)
            {
                try { handler(windows, added); }
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OnmyojiTools.Audio;
using OnmyojiTools.Core;
using OnmyojiTools.Diagnostics;

namespace OnmyojiTools.App
{
    /// <summary>
    /// 应用编排层：持有服务与设置，串行化长时间操作，向界面暴露状态事件。
    /// 界面不直接接触 Win32/COM，所有可取消操作都经过这里。
    /// </summary>
    internal sealed class AppController : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly IGameProcessService _processes;
        private readonly IGameWindowService _windows;
        private readonly IGameLauncher _launcher;
        private readonly IWindowArranger _arranger;
        private readonly IGameShutdownService _closer;
        private readonly SoundFocusService _sound;
        private readonly AppLogger _logger;
        private readonly object _gate = new object();

        private CancellationTokenSource _operationCancellation;
        private string _operation;
        private bool _disposed;

        public event EventHandler StateChanged;

        public AppController(
            AppSettings settings,
            SettingsStore store,
            IGameProcessService processes,
            IGameWindowService windows,
            IGameLauncher launcher,
            IWindowArranger arranger,
            IGameShutdownService closer,
            SoundFocusService sound,
            AppLogger logger)
        {
            _settings = settings;
            _store = store;
            _processes = processes;
            _windows = windows;
            _launcher = launcher;
            _arranger = arranger;
            _closer = closer;
            _sound = sound;
            _logger = logger;
        }

        public static AppController CreateDefault(AppSettings settings, SettingsStore store, AppLogger logger)
        {
            GameProcessService processes = new GameProcessService();
            GameWindowService windows = new GameWindowService();
            AudioSessionService audio = new AudioSessionService();
            SoundFocusService sound = new SoundFocusService(processes, audio, logger);
            return new AppController(
                settings, store, processes, windows,
                new GameLauncher(processes),
                new WindowArrangementService(windows),
                new GameShutdownService(processes),
                sound, logger);
        }

        public AppSettings Settings { get { return _settings; } }

        public bool IsBusy
        {
            get { lock (_gate) return _operation != null; }
        }

        public string CurrentOperation
        {
            get { lock (_gate) return _operation; }
        }

        public bool SoundFocusEnabled { get { return _settings.SoundFocusEnabled; } }

        public void Start()
        {
            if (_settings.SoundFocusEnabled) _sound.Start();
        }

        public int GetRunningCount()
        {
            try { return _processes.CountRunningGames(); }
            catch (Exception ex)
            {
                _logger.LogError("读取运行实例数失败", ex);
                return 0;
            }
        }

        public Task<IReadOnlyList<GameWindowInfo>> GetWindowsAsync()
        {
            return Task.Run(delegate { return _windows.GetGameWindows(); });
        }

        public Task<LaunchResult> LaunchAsync(int target)
        {
            _settings.TargetInstances = AppSettings.Clamp(target, AppSettings.MinInstances, AppSettings.MaxInstances);
            SaveSettings();

            return RunOperationAsync("launch", delegate(CancellationToken token)
            {
                string root = _settings.GameRoot;
                return Task.Run(async delegate
                {
                    return await _launcher.LaunchAsync(target, root, _logger, token);
                });
            });
        }

        public Task<ArrangeResult> ArrangeAsync(int columns)
        {
            _settings.Columns = AppSettings.Clamp(columns, AppSettings.MinColumns, AppSettings.MaxColumns);
            SaveSettings();

            return RunOperationAsync("arrange", delegate(CancellationToken token)
            {
                int value = _settings.Columns;
                return Task.Run(async delegate
                {
                    return await _arranger.ArrangeAsync(value, _logger, token);
                });
            });
        }

        public Task<CloseResult> CloseAllAsync()
        {
            return RunOperationAsync("close", delegate(CancellationToken token)
            {
                return Task.Run(async delegate
                {
                    return await _closer.CloseAllAsync(_logger, token);
                });
            });
        }

        public void CancelCurrentOperation()
        {
            CancellationTokenSource source;
            lock (_gate) source = _operationCancellation;
            if (source == null) return;
            try { source.Cancel(); }
            catch { }
        }

        public bool TrySetGameRoot(string root, out string error)
        {
            string executable;
            if (!InputValidation.TryValidateGameRoot(root, out executable, out error)) return false;

            _settings.GameRoot = Path.GetFullPath(root.Trim());
            SaveSettings();
            _logger.Log("游戏目录已设置为：" + _settings.GameRoot);
            return true;
        }

        public void UpdateTargetInstances(int target)
        {
            int value = AppSettings.Clamp(target, AppSettings.MinInstances, AppSettings.MaxInstances);
            if (_settings.TargetInstances == value) return;
            _settings.TargetInstances = value;
            SaveSettings();
        }

        public void UpdateColumns(int columns)
        {
            int value = AppSettings.Clamp(columns, AppSettings.MinColumns, AppSettings.MaxColumns);
            if (_settings.Columns == value) return;
            _settings.Columns = value;
            SaveSettings();
        }

        public void SetSoundFocusEnabled(bool enabled)
        {
            if (_disposed || _settings.SoundFocusEnabled == enabled) return;
            _settings.SoundFocusEnabled = enabled;
            SaveSettings();

            if (enabled) _sound.Start();
            else _sound.Stop();

            RaiseStateChanged();
        }

        private async Task<T> RunOperationAsync<T>(string name, Func<CancellationToken, Task<T>> body)
            where T : class, IOperationResult, new()
        {
            CancellationTokenSource cancellation;
            lock (_gate)
            {
                if (_disposed) return new T { Error = "程序正在关闭。", Canceled = true };
                if (_operation != null) return new T { Error = "已有操作正在进行，请先取消或等待完成。" };
                _operation = name;
                cancellation = new CancellationTokenSource();
                _operationCancellation = cancellation;
            }

            RaiseStateChanged();
            try
            {
                T result = await body(cancellation.Token);
                return result ?? new T { Error = "操作未返回结果。" };
            }
            catch (OperationCanceledException)
            {
                return new T { Canceled = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(OperationName(name) + "失败", ex);
                return new T { Error = ex.Message };
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_operationCancellation, cancellation))
                    {
                        _operationCancellation = null;
                        _operation = null;
                    }
                }
                cancellation.Dispose();
                RaiseStateChanged();
            }
        }

        private void SaveSettings()
        {
            string error;
            if (!_store.TrySave(_settings, out error))
                _logger.Log("保存配置失败：" + error);
        }

        private void RaiseStateChanged()
        {
            EventHandler handler = StateChanged;
            if (handler == null) return;
            try { handler(this, EventArgs.Empty); }
            catch { }
        }

        private static string OperationName(string operation)
        {
            if (operation == "launch") return "多开";
            if (operation == "arrange") return "窗口排列";
            if (operation == "close") return "关闭窗口";
            return "操作";
        }

        public void Dispose()
        {
            CancellationTokenSource cancellation;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                cancellation = _operationCancellation;
                _operationCancellation = null;
                _operation = null;
            }

            if (cancellation != null)
            {
                try { cancellation.Cancel(); }
                catch { }
            }

            try { _sound.Dispose(); }
            catch (Exception ex) { _logger.LogError("停止声音监控失败", ex); }
        }
    }
}

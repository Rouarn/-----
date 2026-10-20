using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OnmyojiTools.Audio;
using OnmyojiTools.Core;
using OnmyojiTools.Diagnostics;
using OnmyojiTools.Interop;

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
        private readonly IGlobalHotkeyService _hotkeys;
        private readonly IWindowCommandService _windowCommands;
        private readonly IMonitorService _monitors;
        private readonly LayoutPresetStore _layoutPresets;
        private readonly WindowPlacementStore _placements;
        private readonly InstanceLabelStore _labels;
        private readonly IHealthMonitor _health;
        private readonly IWindowWatchService _windowWatch;
        private readonly SoundFocusService _sound;
        private readonly AppLogger _logger;
        private readonly object _gate = new object();

        private CancellationTokenSource _operationCancellation;
        private string _operation;
        private int _autoArrangeGeneration;
        private readonly object _autoRestartGate = new object();
        private readonly Dictionary<int, int> _unresponsiveCounts = new Dictionary<int, int>();
        private readonly HashSet<int> _restarting = new HashSet<int>();
        private readonly Dictionary<int, DateTime> _lastRestart = new Dictionary<int, DateTime>();
        private bool _disposed;

        public event EventHandler StateChanged;

        /// <summary>前台焦点变化（PID），由声音跟随服务的 500ms 检测统一提供。</summary>
        public event Action<int> ForegroundChanged;

        /// <summary>老板键被按下；界面已经通过 WindowsVisibilityChanged 刷新。</summary>
        public event Action BossKeyPressed;

        /// <summary>请求聚焦第 N 个实例（1 起）。</summary>
        public event Action<int> FocusInstanceRequested;

        /// <summary>请求循环切换实例：1 下一个，-1 上一个。</summary>
        public event Action<int> CycleInstanceRequested;

        /// <summary>隐藏/恢复窗口后触发，界面据此刷新列表与计数。</summary>
        public event Action WindowsVisibilityChanged;

        /// <summary>实例健康状态变化；可能在后台线程触发。</summary>
        public event Action<IReadOnlyList<InstanceHealth>> HealthUpdated;

        /// <summary>窗口集合新增/移除；界面据此自动刷新列表。</summary>
        public event Action WindowsChanged;

        public AppController(
            AppSettings settings,
            SettingsStore store,
            IGameProcessService processes,
            IGameWindowService windows,
            IGameLauncher launcher,
            IWindowArranger arranger,
            IGameShutdownService closer,
            IGlobalHotkeyService hotkeys,
            IWindowCommandService windowCommands,
            IMonitorService monitors,
            LayoutPresetStore layoutPresets,
            IHealthMonitor health,
            IWindowWatchService windowWatch,
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
            _hotkeys = hotkeys;
            _windowCommands = windowCommands;
            _monitors = monitors;
            _layoutPresets = layoutPresets;
            _placements = new WindowPlacementStore(
                Path.Combine(Path.GetDirectoryName(store.FilePath) ?? "", "placements.ini"));
            _labels = new InstanceLabelStore(
                Path.Combine(Path.GetDirectoryName(store.FilePath) ?? "", "labels.ini"));
            _health = health;
            _windowWatch = windowWatch;
            _sound = sound;
            _logger = logger;

            _sound.ForegroundChanged += delegate(int processId)
            {
                Action<int> handler = ForegroundChanged;
                if (handler == null) return;
                try { handler(processId); }
                catch { }
            };

            _hotkeys.BossKeyPressed += delegate
            {
                Action handler = BossKeyPressed;
                if (handler == null) return;
                try { handler(); }
                catch { }
            };
            _hotkeys.FocusRequested += delegate(int index)
            {
                Action<int> handler = FocusInstanceRequested;
                if (handler == null) return;
                try { handler(index); }
                catch { }
            };
            _hotkeys.CycleRequested += delegate(int direction)
            {
                Action<int> handler = CycleInstanceRequested;
                if (handler == null) return;
                try { handler(direction); }
                catch { }
            };

            _health.HealthUpdated += delegate(IReadOnlyList<InstanceHealth> snapshot)
            {
                Action<IReadOnlyList<InstanceHealth>> handler = HealthUpdated;
                if (handler != null)
                {
                    try { handler(snapshot); }
                    catch { }
                }
                ProcessAutoRestart(snapshot);
            };

            _windowWatch.WindowsChanged += delegate(IReadOnlyList<GameWindowInfo> snapshot, bool added)
            {
                Action handler = WindowsChanged;
                if (handler != null)
                {
                    try { handler(); }
                    catch { }
                }
                if (added && _settings.AutoArrangeNewWindows) ScheduleAutoArrange();
            };

            if (!string.IsNullOrEmpty(store.LastLoadError))
                _logger.Log("读取配置失败，已回退到默认设置：" + store.LastLoadError);
        }

        public static AppController CreateDefault(AppSettings settings, SettingsStore store, AppLogger logger)
        {
            GameProcessService processes = new GameProcessService();
            GameWindowService windows = new GameWindowService();
            MonitorService monitors = new MonitorService();
            AudioSessionService audio = new AudioSessionService();
            SoundFocusService sound = new SoundFocusService(processes, audio, logger,
                settings.SoundFocusEnabled, settings.BackgroundVolumePercent / 100f);
            string presetPath = Path.Combine(Path.GetDirectoryName(store.FilePath) ?? "", "layouts.ini");
            return new AppController(
                settings, store, processes, windows,
                new GameLauncher(processes, windows),
                new WindowArrangementService(windows, monitors),
                new GameShutdownService(processes),
                new GlobalHotkeyService(logger, settings.HotkeyScheme),
                new WindowCommandService(),
                monitors,
                new LayoutPresetStore(presetPath),
                new HealthMonitorService(windows, logger),
                new WindowWatchService(windows, logger),
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

        /// <summary>最近一次检测到的前台窗口 PID；0 表示没有前台窗口。</summary>
        public int ForegroundProcessId { get { return _sound.ForegroundProcessId; } }

        public void Start()
        {
            // 显示器数量可能变化：启动时把持久化的索引收回到有效范围。
            IReadOnlyList<MonitorInfo> monitors = _monitors.GetMonitors();
            if (_settings.MonitorIndex >= monitors.Count)
            {
                _settings.MonitorIndex = 0;
                SaveSettings();
            }

            // 检测循环始终启动：即使声音跟随关闭，也要为 04 列表提供焦点状态。
            _sound.Start();
            _hotkeys.Start();
            _health.Start();
            _windowWatch.Start();
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

        public int HiddenWindowCount
        {
            get { return _windowCommands.HiddenCount; }
        }

        /// <summary>老板键：有隐藏窗口时恢复，否则隐藏当前所有游戏窗口。返回受影响窗口数。</summary>
        public int ToggleBossKey()
        {
            if (_disposed) return 0;

            int hidden = _windowCommands.HiddenCount;
            if (hidden > 0)
            {
                int shown = _windowCommands.ShowAll();
                _logger.Log(shown > 0
                    ? "老板键：已恢复 " + shown + " 个游戏窗口。"
                    : "老板键：没有可恢复的游戏窗口。");
                RaiseWindowsVisibilityChanged();
                return shown;
            }

            IReadOnlyList<GameWindowInfo> windows;
            try { windows = _windows.GetGameWindows(); }
            catch (Exception ex)
            {
                _logger.LogError("老板键读取游戏窗口失败", ex);
                return 0;
            }

            int count = _windowCommands.HideAll(windows);
            _logger.Log(count > 0
                ? "老板键：已隐藏 " + count + " 个游戏窗口，再按 Ctrl+Alt+H 恢复。"
                : "老板键：没有可隐藏的游戏窗口。");
            RaiseWindowsVisibilityChanged();
            return count;
        }

        public int ShowHiddenWindows()
        {
            if (_disposed) return 0;
            int shown = _windowCommands.ShowAll();
            _logger.Log(shown > 0
                ? "已恢复 " + shown + " 个隐藏的游戏窗口。"
                : "没有隐藏的游戏窗口。");
            RaiseWindowsVisibilityChanged();
            return shown;
        }

        public int HideAllWindows()
        {
            if (_disposed) return 0;
            IReadOnlyList<GameWindowInfo> windows;
            try { windows = _windows.GetGameWindows(); }
            catch (Exception ex)
            {
                _logger.LogError("读取游戏窗口失败", ex);
                return 0;
            }

            int count = _windowCommands.HideAll(windows);
            _logger.Log(count > 0
                ? "已隐藏 " + count + " 个游戏窗口。"
                : "没有可隐藏的游戏窗口。");
            RaiseWindowsVisibilityChanged();
            return count;
        }

        public bool HideWindow(IntPtr handle)
        {
            if (_disposed || handle == IntPtr.Zero) return false;
            bool ok = _windowCommands.Hide(handle);
            if (ok)
            {
                _logger.Log("已隐藏一个游戏窗口。");
                RaiseWindowsVisibilityChanged();
            }
            return ok;
        }

        public bool FocusWindow(IntPtr handle)
        {
            if (_disposed || handle == IntPtr.Zero) return false;
            bool ok = _windowCommands.Focus(handle);
            if (!ok) _logger.Log("聚焦游戏窗口失败，可能窗口已关闭或权限不足。");
            return ok;
        }

        public bool MinimizeWindow(IntPtr handle)
        {
            if (_disposed || handle == IntPtr.Zero) return false;
            bool ok = _windowCommands.Minimize(handle);
            if (!ok) _logger.Log("最小化游戏窗口失败，可能窗口已关闭。");
            return ok;
        }

        public bool SwapWindowPositions(IntPtr first, IntPtr second)
        {
            if (_disposed || first == IntPtr.Zero || second == IntPtr.Zero || first == second) return false;
            if (!NativeMethods.IsWindow(first) || !NativeMethods.IsWindow(second)) return false;

            NativeMethods.RECT a;
            NativeMethods.RECT b;
            if (!NativeMethods.GetWindowRect(first, out a) || !NativeMethods.GetWindowRect(second, out b)) return false;

            try
            {
                bool firstMoved = NativeMethods.SetWindowPos(first, IntPtr.Zero,
                    b.Left, b.Top, a.Width, a.Height,
                    NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                bool secondMoved = NativeMethods.SetWindowPos(second, IntPtr.Zero,
                    a.Left, a.Top, b.Width, b.Height,
                    NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                if (firstMoved && secondMoved)
                {
                    _logger.Log("已交换两个实例的窗口位置。");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Log("交换窗口位置失败：" + ex.Message);
            }
            return false;
        }

        public void SetInstanceVolume(int processId, float volume)
        {
            if (_disposed) return;
            _sound.SetInstanceVolume(processId, volume);
        }

        public void ClearInstanceVolume(int processId)
        {
            if (_disposed) return;
            _sound.ClearInstanceVolume(processId);
        }

        public Task<CloseResult> CloseInstanceAsync(int processId)
        {
            return RunOperationAsync("close-one", delegate(CancellationToken token)
            {
                return Task.Run(async delegate
                {
                    return await _closer.CloseAsync(processId, _logger, token);
                });
            });
        }

        public Task<LaunchResult> RestartInstanceAsync(int processId)
        {
            return RunOperationAsync("restart", delegate(CancellationToken token)
            {
                return Task.Run(async delegate
                {
                    int target = _processes.CountRunningGames();
                    if (target < 1) target = 1;

                    CloseResult close = await _closer.CloseAsync(processId, _logger, token);
                    if (close.Error != null)
                        return new LaunchResult { Error = close.Error };
                    if (close.Canceled)
                        return new LaunchResult { Canceled = true };
                    if (close.Failed > 0 && close.Graceful + close.Forced == 0)
                        return new LaunchResult { Error = "实例关闭失败，已取消重启。" };

                    await Task.Delay(400, token);
                    return await _launcher.LaunchAsync(target, _settings.GameRoot, _logger, token);
                });
            });
        }

        public IReadOnlyList<LayoutPreset> GetLayoutPresets()
        {
            return _layoutPresets.Load();
        }

        public bool SaveLayoutPreset(string name, int columns, int monitorIndex, out string error)
        {
            error = null;
            if (!LayoutPresetStore.IsValidName(name))
            {
                error = "预设名称需为 1–20 个字符，且不能包含方括号或换行。";
                return false;
            }

            List<LayoutPreset> presets = _layoutPresets.Load();
            string clean = name.Trim();
            bool replaced = false;
            for (int i = 0; i < presets.Count; i++)
            {
                if (!string.Equals(presets[i].Name, clean, StringComparison.OrdinalIgnoreCase)) continue;
                presets[i].Columns = AppSettings.Clamp(columns, AppSettings.MinColumns, AppSettings.MaxColumns);
                presets[i].MonitorIndex = monitorIndex < 0 ? 0 : monitorIndex;
                replaced = true;
                break;
            }
            if (!replaced)
            {
                if (presets.Count >= 12)
                {
                    error = "最多保存 12 个布局预设。";
                    return false;
                }
                presets.Add(new LayoutPreset
                {
                    Name = clean,
                    Columns = AppSettings.Clamp(columns, AppSettings.MinColumns, AppSettings.MaxColumns),
                    MonitorIndex = monitorIndex < 0 ? 0 : monitorIndex
                });
            }

            if (!_layoutPresets.Save(presets, out error)) return false;
            _logger.Log("布局预设已保存：" + clean + "（" + columns + " 列，显示器 " + (monitorIndex + 1) + "）。");
            return true;
        }

        public bool DeleteLayoutPreset(string name, out string error)
        {
            error = null;
            List<LayoutPreset> presets = _layoutPresets.Load();
            bool removed = false;
            for (int i = presets.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(presets[i].Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                presets.RemoveAt(i);
                removed = true;
            }
            if (!removed)
            {
                error = "未找到该布局预设。";
                return false;
            }
            if (!_layoutPresets.Save(presets, out error)) return false;
            _logger.Log("布局预设已删除：" + name);
            return true;
        }

        private void RaiseWindowsVisibilityChanged()
        {
            Action handler = WindowsVisibilityChanged;
            if (handler == null) return;
            try { handler(); }
            catch { }
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
                int monitor = _settings.MonitorIndex;
                return Task.Run(async delegate
                {
                    return await _arranger.ArrangeAsync(value, monitor, _logger, token);
                });
            });
        }

        /// <summary>一键会话：补足实例 → 等待窗口就绪 → 按当前列数/显示器排列。</summary>
        public Task<SessionResult> StartSessionAsync(int target, int columns)
        {
            int targetValue = AppSettings.Clamp(target, AppSettings.MinInstances, AppSettings.MaxInstances);
            int columnsValue = AppSettings.Clamp(columns, AppSettings.MinColumns, AppSettings.MaxColumns);
            _settings.TargetInstances = targetValue;
            _settings.Columns = columnsValue;
            SaveSettings();

            return RunOperationAsync("session", delegate(CancellationToken token)
            {
                int monitor = _settings.MonitorIndex;
                string root = _settings.GameRoot;
                return Task.Run(async delegate
                {
                    SessionResult result = new SessionResult { Target = targetValue, Columns = columnsValue };
                    result.Launch = await _launcher.LaunchAsync(targetValue, root, _logger, token);
                    if (result.Launch == null)
                    {
                        result.Error = "多开步骤没有返回结果。";
                        return result;
                    }
                    if (result.Launch.Canceled)
                    {
                        result.Canceled = true;
                        return result;
                    }
                    if (!string.IsNullOrEmpty(result.Launch.Error))
                    {
                        result.Error = result.Launch.Error;
                        return result;
                    }

                    result.Arrange = await _arranger.ArrangeAsync(columnsValue, monitor, _logger, token);
                    if (result.Arrange == null)
                    {
                        result.Error = "排列步骤没有返回结果。";
                        return result;
                    }
                    if (result.Arrange.Canceled) result.Canceled = true;
                    if (!string.IsNullOrEmpty(result.Arrange.Error)) result.Error = result.Arrange.Error;
                    return result;
                });
            });
        }

        public IReadOnlyList<MonitorInfo> GetMonitors()
        {
            return _monitors.GetMonitors();
        }

        public void SetMonitorIndex(int index)
        {
            IReadOnlyList<MonitorInfo> monitors = _monitors.GetMonitors();
            if (monitors.Count == 0) return;
            int value = index < 0 || index >= monitors.Count ? 0 : index;
            if (_settings.MonitorIndex == value) return;
            _settings.MonitorIndex = value;
            SaveSettings();
            _logger.Log("排列目标已切换为显示器 " + (value + 1) + "。");
        }

        public void SetBackgroundVolume(int percent)
        {
            int value = AppSettings.Clamp(percent, 0, 100);
            if (_settings.BackgroundVolumePercent == value) return;
            _settings.BackgroundVolumePercent = value;
            SaveSettings();
            _sound.SetBackgroundVolume(value / 100f);
            _logger.Log("失焦音量已设为 " + value + "%。");
        }

        public void SetAutoArrangeNewWindows(bool enabled)
        {
            if (_settings.AutoArrangeNewWindows == enabled) return;
            _settings.AutoArrangeNewWindows = enabled;
            SaveSettings();
            _logger.Log(enabled
                ? "已开启新窗口自动排列；检测到新增实例时会按当前列数排列。"
                : "已关闭新窗口自动排列。");
        }

        public void SetAutoRestartUnresponsive(bool enabled)
        {
            if (_settings.AutoRestartUnresponsive == enabled) return;
            _settings.AutoRestartUnresponsive = enabled;
            SaveSettings();
            lock (_autoRestartGate)
            {
                _unresponsiveCounts.Clear();
                if (!enabled) _lastRestart.Clear();
            }
            _logger.Log(enabled
                ? "已开启无响应自动重启（默认 30 秒内最多一次，需自行承担重启风险）。"
                : "已关闭无响应自动重启。");
        }

        public void SetCompactMode(bool enabled)
        {
            if (_settings.CompactMode == enabled) return;
            _settings.CompactMode = enabled;
            SaveSettings();
            _logger.Log(enabled ? "已切换为紧凑模式。" : "已恢复标准模式。");
        }

        public void SetDimUnfocusedWindows(bool enabled)
        {
            if (_settings.DimUnfocusedWindows == enabled) return;
            _settings.DimUnfocusedWindows = enabled;
            SaveSettings();
            _logger.Log(enabled
                ? "已开启非焦点窗口半透明；若客户端显示异常请立即关闭。"
                : "已关闭非焦点窗口半透明。");
        }

        public void SetDarkTheme(bool enabled)
        {
            if (_settings.DarkTheme == enabled) return;
            _settings.DarkTheme = enabled;
            SaveSettings();
            _logger.Log(enabled ? "已切换为深色主题。" : "已切换为浅色主题。");
        }

        private void ProcessAutoRestart(IReadOnlyList<InstanceHealth> health)
        {
            if (!_settings.AutoRestartUnresponsive || health == null || _disposed) return;
            if (IsBusy) return;

            List<int> candidates = new List<int>();
            DateTime now = DateTime.UtcNow;
            lock (_autoRestartGate)
            {
                for (int i = 0; i < health.Count; i++)
                {
                    int processId = health[i].ProcessId;
                    if (health[i].Responsive)
                    {
                        _unresponsiveCounts.Remove(processId);
                        continue;
                    }

                    int count;
                    _unresponsiveCounts.TryGetValue(processId, out count);
                    count++;
                    _unresponsiveCounts[processId] = count;
                    if (count < 2 || _restarting.Contains(processId)) continue;

                    DateTime last;
                    if (_lastRestart.TryGetValue(processId, out last) && (now - last).TotalSeconds < 30) continue;
                    _restarting.Add(processId);
                    _lastRestart[processId] = now;
                    candidates.Add(processId);
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                int processId = candidates[i];
                _logger.Log("实例 PID " + processId + " 连续无响应，正在自动重启…");
                Task<LaunchResult> task = RestartInstanceAsync(processId);
                task.ContinueWith(delegate(Task<LaunchResult> completed)
                {
                    lock (_autoRestartGate) _restarting.Remove(processId);
                    if (completed.Exception != null)
                        _logger.LogError("自动重启失败", completed.Exception.GetBaseException());
                    else if (completed.Result != null && !string.IsNullOrEmpty(completed.Result.Error))
                        _logger.Log("自动重启失败：" + completed.Result.Error);
                });
            }
        }

        private void ScheduleAutoArrange()
        {
            int generation = Interlocked.Increment(ref _autoArrangeGeneration);
            Task.Delay(1200).ContinueWith(delegate
            {
                if (generation != _autoArrangeGeneration || _disposed) return;
                if (!_settings.AutoArrangeNewWindows || IsBusy) return;
                _logger.Log("检测到新增窗口，正在自动排列…");
                Task task = ArrangeAsync(_settings.Columns);
                task.ContinueWith(delegate(Task completed)
                {
                    Exception ex = completed.Exception == null ? null : completed.Exception.GetBaseException();
                    if (ex != null) _logger.LogError("自动排列失败", ex);
                }, TaskContinuationOptions.OnlyOnFaulted);
            });
        }

        public bool StartWithWindowsEnabled
        {
            get { return StartupManager.IsEnabled(); }
        }

        public void SetHotkeyScheme(int scheme)
        {
            int value = AppSettings.Clamp(scheme, 0, 2);
            if (_settings.HotkeyScheme == value) return;
            _settings.HotkeyScheme = value;
            SaveSettings();
            _hotkeys.SetScheme(value);
            string name = value == 1 ? "Ctrl+Shift" : (value == 2 ? "Alt+Shift" : "Ctrl+Alt");
            _logger.Log("全局快捷键方案已切换为 " + name + "。");
        }

        public bool SetStartWithWindows(bool enabled, out string error)
        {
            bool ok = StartupManager.SetEnabled(enabled, out error);
            if (ok) _logger.Log(enabled ? "已开启开机自启。" : "已关闭开机自启。");
            else _logger.Log("设置开机自启失败：" + error);
            return ok;
        }

        public string GetInstanceLabel(string title)
        {
            return _labels.Get(title);
        }

        public void SetInstanceLabel(string title, string label)
        {
            string error;
            if (!_labels.Set(title, label, out error))
            {
                _logger.Log("保存实例标签失败：" + error);
                return;
            }
            _logger.Log(string.IsNullOrWhiteSpace(label)
                ? "已清除实例标签：" + title
                : "实例标签已保存：" + title + " → " + label.Trim());
        }

        public int SaveWindowPlacements()
        {
            IReadOnlyList<GameWindowInfo> windows;
            try { windows = _windows.GetGameWindows(); }
            catch (Exception ex)
            {
                _logger.LogError("读取窗口位置失败", ex);
                return 0;
            }

            string error;
            if (!_placements.Save(windows, out error))
            {
                _logger.Log("保存窗口位置失败：" + error);
                return 0;
            }

            int count = 0;
            for (int i = 0; i < windows.Count; i++)
            {
                if (!windows[i].Bounds.IsEmpty) count++;
            }
            _logger.Log("已记住 " + count + " 个窗口的位置与大小。");
            return count;
        }

        public int RestoreWindowPlacements()
        {
            IReadOnlyList<GameWindowInfo> windows;
            try { windows = _windows.GetGameWindows(); }
            catch (Exception ex)
            {
                _logger.LogError("读取窗口位置失败", ex);
                return 0;
            }

            string error;
            int count = _placements.Apply(windows, out error);
            if (!string.IsNullOrEmpty(error)) _logger.Log("恢复窗口位置时出现问题：" + error);
            _logger.Log(count > 0
                ? "已恢复 " + count + " 个窗口的位置。"
                : "没有找到可匹配的窗口位置记录。");
            return count;
        }

        public bool ExportConfiguration(string folder, out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(folder))
                {
                    error = "请选择导出目录。";
                    return false;
                }
                Directory.CreateDirectory(folder);
                File.Copy(_store.FilePath, Path.Combine(folder, "config.ini"), true);
                if (File.Exists(_layoutPresets.FilePath))
                    File.Copy(_layoutPresets.FilePath, Path.Combine(folder, "layouts.ini"), true);

                _logger.Log("配置已导出到：" + folder);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _logger.Log("导出配置失败：" + error);
                return false;
            }
        }

        public bool ImportConfiguration(string folder, out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(folder))
                {
                    error = "请选择包含 config.ini 的目录。";
                    return false;
                }
                string sourceConfig = Path.Combine(folder, "config.ini");
                if (!File.Exists(sourceConfig))
                {
                    error = "该目录下没有 config.ini。";
                    return false;
                }

                File.Copy(sourceConfig, _store.FilePath, true);
                string sourceLayouts = Path.Combine(folder, "layouts.ini");
                if (File.Exists(sourceLayouts))
                    File.Copy(sourceLayouts, _layoutPresets.FilePath, true);

                // 把导入值同步到内存，避免关闭窗口时又用旧设置覆盖备份。
                AppSettings imported = _store.Load();
                _settings.GameRoot = imported.GameRoot;
                _settings.TargetInstances = imported.TargetInstances;
                _settings.Columns = imported.Columns;
                _settings.MonitorIndex = imported.MonitorIndex;
                _settings.BackgroundVolumePercent = imported.BackgroundVolumePercent;
                _settings.SoundFocusEnabled = imported.SoundFocusEnabled;
                _sound.VolumeControlEnabled = imported.SoundFocusEnabled;
                _sound.SetBackgroundVolume(imported.BackgroundVolumePercent / 100f);

                _logger.Log("配置已导入：" + folder + "，重启工具后完整生效。");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _logger.Log("导入配置失败：" + error);
                return false;
            }
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

            _sound.VolumeControlEnabled = enabled;
            _logger.Log(enabled
                ? "声音跟随聚焦已开启：聚焦窗口正常音量，失焦窗口自动调小。"
                : "声音跟随聚焦已关闭，已恢复游戏音量。");

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
            if (operation == "close-one") return "关闭实例";
            if (operation == "restart") return "重启实例";
            if (operation == "session") return "一键会话";
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
            try { _hotkeys.Dispose(); }
            catch (Exception ex) { _logger.LogError("停止全局快捷键失败", ex); }
            try { _health.Dispose(); }
            catch (Exception ex) { _logger.LogError("停止健康监控失败", ex); }
            try { _windowWatch.Dispose(); }
            catch (Exception ex) { _logger.LogError("停止窗口变化检测失败", ex); }
        }
    }
}

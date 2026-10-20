using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OnmyojiTools.App;
using OnmyojiTools.Audio;
using OnmyojiTools.Core;
using OnmyojiTools.Diagnostics;
using OnmyojiTools.Interop;
using OnmyojiTools.Ui;

/// <summary>
/// 开发辅助：用假服务构造可重复的 UI 状态，离屏截图并做交互冒烟测试。
/// 不参与正式构建，也不会操作真实的游戏窗口/音量。
/// </summary>
internal static class UiShot
{
    [STAThread]
    private static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : ".";
        string tag = args.Length > 1 ? args[1] : "ui";
        Directory.CreateDirectory(outDir);

        try { NativeMethods.SetProcessDpiAwareness(2); } catch { }
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        Application app = new Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        int instanceCount = tag == "list" ? 12 : ((tag == "smoke" || tag == "settings" || tag == "settingsbottom" || tag == "hidden" || tag == "resources" || tag == "preview" || tag == "dark" || tag == "theme" || tag == "menu") ? 3 : 0);
        AppSettings settings = new AppSettings
        {
            GameRoot = @"D:\Netease\yys",
            TargetInstances = 2,
            Columns = 2,
            SoundFocusEnabled = true,
            DarkTheme = tag == "dark"
        };
        SettingsStore store = new SettingsStore(Path.Combine(outDir, "config.ini"));
        AppLogger logger = new AppLogger();
        FakeProcessService processes = new FakeProcessService(instanceCount);
        IGameWindowService windows = tag == "realpreview"
            ? (IGameWindowService)new GameWindowService()
            : (IGameWindowService)new FakeWindowService(instanceCount);
        FakeAudioSessionService audio = new FakeAudioSessionService();
        FakeHealthMonitor health = new FakeHealthMonitor();
        FakeWindowWatchService watch = new FakeWindowWatchService();
        FakeArranger arranger = new FakeArranger();
        SoundFocusService sound = new SoundFocusService(processes, audio, logger);
        AppController controller = new AppController(
            settings, store, processes, windows,
            new FakeLauncher(), arranger, new FakeCloser(),
            new FakeHotkeyService(), new FakeWindowCommandService(),
            new FakeMonitorService(),
            new LayoutPresetStore(Path.Combine(outDir, "layouts.ini")),
            health, watch,
            sound, logger);

        MainWindow window = new MainWindow(controller, logger, false);
        window.Show();

        window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async delegate
        {
            try
            {
                await Task.Delay(700);

                if (tag == "about")
                {
                    window.ShowAbout();
                    await Task.Delay(250);
                    Save((FrameworkElement)window.Content, Path.Combine(outDir, "about.png"));
                }
                else if (tag == "confirm")
                {
                    MethodInfo confirm = typeof(MainWindow).GetMethod("ConfirmAsync", BindingFlags.NonPublic | BindingFlags.Instance);
                    confirm.Invoke(window, new object[]
                    {
                        "关闭所有游戏窗口",
                        "将向所有阴阳师窗口发送退出请求，并自动点击退出确认；3 秒内未退出会强制结束进程。是否继续？",
                        "确认关闭"
                    });
                    await Task.Delay(200);
                    Save((FrameworkElement)window.Content, Path.Combine(outDir, "confirm.png"));
                }
                else if (tag == "settings")
                {
                    Invoke(window, "OpenSettings");
                    await Task.Delay(250);
                    Save((FrameworkElement)window.Content, Path.Combine(outDir, "settings.png"));
                }
                else if (tag == "settingsbottom")
                {
                    Invoke(window, "OpenSettings");
                    await Task.Delay(200);
                    Grid settingsGrid = (Grid)Field(window, "_settingsOverlay");
                    ScrollViewer scroll = FindVisualChild<ScrollViewer>(settingsGrid);
                    if (scroll != null) scroll.ScrollToEnd();
                    await Task.Delay(200);
                    Save((FrameworkElement)window.Content, Path.Combine(outDir, "settingsbottom.png"));
                }
                else if (tag == "hidden")
                {
                    controller.ToggleBossKey();
                    await Task.Delay(300);
                    Save((FrameworkElement)window.Content, Path.Combine(outDir, "hidden.png"));
                }
                else if (tag == "resources")
                {
                    Invoke(window, "OpenResources");
                    await Task.Delay(350);
                    Save((FrameworkElement)window.Content, Path.Combine(outDir, "resources.png"));
                }
                else if (tag == "preview")
                {
                    Invoke(window, "OpenPreview");
                    await Task.Delay(350);
                    Save((FrameworkElement)window.Content, Path.Combine(outDir, "preview.png"));
                }
                else if (tag == "theme")
                {
                    controller.SetDarkTheme(true);
                    Invoke(window, "ApplyTheme", true);
                    await Task.Delay(450);
                    Save((FrameworkElement)window.Content, Path.Combine(outDir, "theme.png"));
                }
                else if (tag == "realpreview")
                {
                    Invoke(window, "OpenPreview");
                    await Task.Delay(1800);
                    SaveScreen(window, Path.Combine(outDir, "realpreview.png"));
                }
                else if (tag == "menu")
                {
                    Panel rows = (Panel)Field(window, "_instancePanel");
                    if (rows.Children.Count > 0)
                    {
                        FrameworkElement row = rows.Children[0] as FrameworkElement;
                        ContextMenu menu = row == null ? null : row.ContextMenu;
                        if (menu != null)
                        {
                            menu.PlacementTarget = row;
                            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Left;
                            menu.IsOpen = true;
                        }
                    }
                    await Task.Delay(350);
                    SaveScreen(window, Path.Combine(outDir, "menu.png"));
                }
                else if (tag == "smoke")
                {
                    await RunSmokeAsync(window, controller, audio, health, watch, arranger, outDir);
                }
                else
                {
                    Save((FrameworkElement)window.Content, Path.Combine(outDir, tag + ".png"));
                }
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(outDir, tag + "-error.txt"), ex.ToString()); } catch { }
            }
            finally
            {
                app.Shutdown();
            }
        }));

        app.Run();
    }

    private static async Task RunSmokeAsync(MainWindow window, AppController controller,
        FakeAudioSessionService audio, FakeHealthMonitor health, FakeWindowWatchService watch,
        FakeArranger arranger, string outDir)
    {
        StringBuilder report = new StringBuilder();
        int passed = 0;
        int failed = 0;
        Action<string, bool> check = delegate(string name, bool ok)
        {
            report.AppendLine((ok ? "PASS  " : "FAIL  ") + name);
            if (ok) passed++; else failed++;
        };

        try
        {
            TextBox log = (TextBox)Field(window, "txtLog");
            TextBox root = (TextBox)Field(window, "txtRoot");
            TextBox openNum = (TextBox)Field(window, "txtOpenNum");
            TextBlock winCount = (TextBlock)Field(window, "lblWinCount");
            TextBlock soundState = (TextBlock)Field(window, "lblSoundState");
            SwitchControl soundSwitch = (SwitchControl)Field(window, "swSound");
            Border busyPanel = (Border)Field(window, "_busyPanel");
            Grid aboutOverlay = (Grid)Field(window, "_aboutOverlay");
            Grid confirmOverlay = (Grid)Field(window, "_confirmOverlay");
            Grid settingsOverlay = (Grid)Field(window, "_settingsOverlay");
            Grid resourcesOverlay = (Grid)Field(window, "_resourcesOverlay");
            Grid previewOverlay = (Grid)Field(window, "_previewOverlay");

            check("窗口内容已构建", window.Content is FrameworkElement);
            check("启动日志已写入", log.Text.Contains("控制台已就绪"));
            check("游戏目录已显示", root.Text.Length > 0);
            check("实例数量已刷新", winCount.Text.Contains("3 个窗口"));
            check("标题计数与列表一致",
                ((TextBlock)Field(window, "lblRunning")).Text.Trim() == "3" && winCount.Text.Contains("3 个窗口"));
            check("实例行已生成", ((Panel)Field(window, "_instancePanel")).Children.Count == 3);

            Invoke(window, "UpdateFocusStatus", 2000);
            string firstFocused = GetRowState(window, 2000);
            string secondRunning = GetRowState(window, 2001);
            Invoke(window, "UpdateFocusStatus", 2001);
            string firstRunning = GetRowState(window, 2000);
            string secondFocused = GetRowState(window, 2001);
            check("状态列跟随焦点切换",
                firstFocused == "聚焦" && secondRunning == "运行中" &&
                secondFocused == "聚焦" && firstRunning == "运行中");

            health.Trigger(2000, false);
            await Task.Delay(80);
            Invoke(window, "UpdateFocusStatus", 2000);
            check("健康状态标记无响应", GetRowState(window, 2000) == "无响应");
            health.Trigger(2000, true);
            await Task.Delay(80);
            Invoke(window, "UpdateFocusStatus", 2000);
            check("健康状态恢复", GetRowState(window, 2000) == "聚焦");

            Invoke(window, "StepOpen", 1);
            check("步进器 +", openNum.Text == "3");
            Invoke(window, "StepOpen", -1);
            check("步进器 -", openNum.Text == "2");

            Invoke(window, "SetArrangeCols", 4, true);
            check("列数切换并持久化", controller.Settings.Columns == 4);
            Invoke(window, "SetArrangeCols", 2, true);
            check("列数恢复", controller.Settings.Columns == 2);

            check("显示器枚举", controller.GetMonitors().Count >= 1);
            controller.SetMonitorIndex(0);
            check("显示器选择", controller.Settings.MonitorIndex == 0);
            controller.SetBackgroundVolume(25);
            check("失焦音量持久化", controller.Settings.BackgroundVolumePercent == 25);
            controller.SetInstanceLabel("阴阳师 - 网易官方版本", "主号");
            await window.RefreshListAsync();
            check("实例标签显示", GetRowCustomLabel(window, 2000) == "主号");
            controller.SetHotkeyScheme(1);
            check("快捷键方案切换", controller.Settings.HotkeyScheme == 1);
            controller.SetHotkeyScheme(0);
            check("窗口位置记忆", controller.SaveWindowPlacements() == 3);

            Invoke(window, "TogglePin");
            bool topmost = window.Topmost;
            Invoke(window, "TogglePin");
            check("置顶双向切换", topmost && !window.Topmost);

            soundSwitch.IsOn = false;
            await Task.Delay(200);
            check("声音开关关闭状态", soundState.Text == "已暂停" && !controller.SoundFocusEnabled);
            soundSwitch.IsOn = true;
            await Task.Delay(700);
            check("声音开关开启状态", soundState.Text == "监控中" && controller.SoundFocusEnabled);
            check("声音监控调用音频服务", audio.ApplyCalls > 0);

            controller.SetInstanceVolume(2000, 0.5f);
            check("单实例音量设置", audio.LastVolumePid == 2000 && Math.Abs(audio.LastVolume - 0.5f) < 0.01f);
            controller.ClearInstanceVolume(2000);

            int hidden = controller.ToggleBossKey();
            check("老板键隐藏实例", hidden == 3 && controller.HiddenWindowCount == 3);
            int shown = controller.ToggleBossKey();
            check("老板键恢复实例", shown == 3 && controller.HiddenWindowCount == 0);

            Invoke(window, "OpenSettings");
            check("设置弹层打开", settingsOverlay.Visibility == Visibility.Visible);
            TextBox presetName = (TextBox)Field(window, "_presetNameBox");
            presetName.Text = "冒烟布局";
            Invoke(window, "SavePreset");
            bool presetFound = false;
            foreach (LayoutPreset item in controller.GetLayoutPresets())
            {
                if (item.Name == "冒烟布局" && item.Columns == 2 && item.MonitorIndex == 0) presetFound = true;
            }
            check("布局预设保存", presetFound);
            Invoke(window, "CloseSettings");
            check("设置弹层关闭", settingsOverlay.Visibility == Visibility.Collapsed);

            Invoke(window, "OpenResources");
            await Task.Delay(250);
            check("资源面板打开", resourcesOverlay.Visibility == Visibility.Visible);
            Invoke(window, "CloseResources");
            check("资源面板关闭", resourcesOverlay.Visibility == Visibility.Collapsed);

            Invoke(window, "OpenPreview");
            await Task.Delay(250);
            check("缩略图预览打开", previewOverlay.Visibility == Visibility.Visible);
            Invoke(window, "ClosePreview");
            check("缩略图预览关闭", previewOverlay.Visibility == Visibility.Collapsed);

            CloseResult singleClose = await controller.CloseInstanceAsync(2000);
            check("单实例关闭编排", singleClose != null && !controller.IsBusy);
            LaunchResult restart = await controller.RestartInstanceAsync(2001);
            check("单实例重启编排", restart != null && !controller.IsBusy);

            Invoke(window, "ShowAbout");
            check("关于弹层打开", aboutOverlay.Visibility == Visibility.Visible);
            Invoke(window, "CloseAbout");
            check("关于弹层关闭", aboutOverlay.Visibility == Visibility.Collapsed);

            MethodInfo confirmMethod = typeof(MainWindow).GetMethod("ConfirmAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Task<bool> confirmTask = (Task<bool>)confirmMethod.Invoke(window, new object[]
            {
                "关闭所有游戏窗口", "测试确认弹层。", "确认关闭"
            });
            check("确认弹层打开", confirmOverlay.Visibility == Visibility.Visible);
            Invoke(window, "FinishConfirm", false);
            bool confirmed = await confirmTask;
            check("确认弹层取消返回 false", !confirmed);
            check("确认弹层关闭", confirmOverlay.Visibility == Visibility.Collapsed);

            Task<LaunchResult> launchTask = controller.LaunchAsync(3);
            await Task.Delay(60);
            check("操作忙状态提示", busyPanel.Visibility == Visibility.Visible);
            LaunchResult launch = await launchTask;
            check("多开编排完成", launch != null && !controller.IsBusy);
            check("操作忙状态复位", busyPanel.Visibility == Visibility.Collapsed);

            SessionResult session = await controller.StartSessionAsync(3, 2);
            check("一键会话编排", session != null && !controller.IsBusy && string.IsNullOrEmpty(session.Error));

            controller.SetAutoArrangeNewWindows(true);
            int arrangeBefore = arranger.ArrangeCalls;
            IReadOnlyList<GameWindowInfo> snapshot = await controller.GetWindowsAsync();
            watch.Trigger(snapshot, true);
            await Task.Delay(1700);
            check("新窗口自动排列", arranger.ArrangeCalls > arrangeBefore);
            controller.SetAutoArrangeNewWindows(false);

            controller.SetAutoRestartUnresponsive(true);
            check("自动重启开关", controller.Settings.AutoRestartUnresponsive);
            controller.SetAutoRestartUnresponsive(false);
            controller.SetCompactMode(true);
            Invoke(window, "ApplyCompactMode", true);
            check("紧凑模式切换", controller.Settings.CompactMode && Math.Abs(window.Width - 1040) < 1);
            controller.SetCompactMode(false);
            Invoke(window, "ApplyCompactMode", true);
            controller.SetDimUnfocusedWindows(true);
            check("窗口透明度开关", controller.Settings.DimUnfocusedWindows);
            controller.SetDimUnfocusedWindows(false);

            Task<LaunchResult> cancelTask = controller.LaunchAsync(3);
            await Task.Delay(60);
            controller.CancelCurrentOperation();
            LaunchResult canceled = await cancelTask;
            check("后台操作可取消", canceled.Canceled && !controller.IsBusy);
            check("取消后忙状态复位", busyPanel.Visibility == Visibility.Collapsed);

            ArrangeResult arrange = await controller.ArrangeAsync(2);
            check("排列编排完成", arrange != null && arrange.MonitorIndex == 0);
            CloseResult close = await controller.CloseAllAsync();
            check("关闭编排完成", close != null);

            string backup = Path.Combine(outDir, "config-backup");
            string configError;
            check("配置导出", controller.ExportConfiguration(backup, out configError));
            check("配置导入", controller.ImportConfiguration(backup, out configError));
            check("设置文件已写入", File.Exists(Path.Combine(outDir, "config.ini")));
        }
        catch (Exception ex)
        {
            failed++;
            report.AppendLine("EXCEPTION  " + ex);
        }

        report.AppendLine("RESULT passed=" + passed + " failed=" + failed);
        File.WriteAllText(Path.Combine(outDir, "smoke.txt"), report.ToString(), new UTF8Encoding(false));
        Save((FrameworkElement)window.Content, Path.Combine(outDir, "smoke.png"));
    }

    private static object Field(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null) throw new MissingFieldException(name);
        return field.GetValue(target);
    }

    private static string GetRowState(MainWindow window, int processId)
    {
        IEnumerable rows = (IEnumerable)Field(window, "_rows");
        foreach (object row in rows)
        {
            Type type = row.GetType();
            int rowPid = (int)type.GetField("ProcessId", BindingFlags.Public | BindingFlags.Instance).GetValue(row);
            if (rowPid != processId) continue;

            TextBlock label = (TextBlock)type.GetField("Label", BindingFlags.Public | BindingFlags.Instance).GetValue(row);
            return label.Text;
        }
        return null;
    }

    private static string GetRowCustomLabel(MainWindow window, int processId)
    {
        IEnumerable rows = (IEnumerable)Field(window, "_rows");
        foreach (object row in rows)
        {
            Type type = row.GetType();
            int rowPid = (int)type.GetField("ProcessId", BindingFlags.Public | BindingFlags.Instance).GetValue(row);
            if (rowPid != processId) continue;
            return (string)type.GetField("CustomLabel", BindingFlags.Public | BindingFlags.Instance).GetValue(row);
        }
        return null;
    }

    private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            T match = child as T;
            if (match != null) return match;
            T nested = FindVisualChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    private static void Invoke(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null) throw new MissingMethodException(name);
        method.Invoke(target, args);
    }

    private static void Save(FrameworkElement root, string path)
    {
        root.UpdateLayout();
        int width = (int)Math.Ceiling(root.ActualWidth);
        int height = (int)Math.Ceiling(root.ActualHeight);
        if (width < 2 || height < 2) throw new InvalidOperationException("empty layout " + width + "x" + height);

        RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        PngBitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream stream = File.Create(path)) encoder.Save(stream);
    }

    private static void SaveScreen(Window window, string path)
    {
        Point origin = window.PointToScreen(new Point(0, 0));
        PresentationSource source = PresentationSource.FromVisual(window);
        double scaleX = 1;
        double scaleY = 1;
        if (source != null && source.CompositionTarget != null)
        {
            scaleX = source.CompositionTarget.TransformToDevice.M11;
            scaleY = source.CompositionTarget.TransformToDevice.M22;
        }
        int width = Math.Max(1, (int)Math.Round(window.ActualWidth * scaleX));
        int height = Math.Max(1, (int)Math.Round(window.ActualHeight * scaleY));
        using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(width, height))
        using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen((int)Math.Round(origin.X), (int)Math.Round(origin.Y), 0, 0,
                new System.Drawing.Size(width, height));
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    // ==================== 假服务 ====================
    private sealed class FakeProcessService : IGameProcessService
    {
        private readonly int _count;

        public FakeProcessService(int count) { _count = count; }

        public HashSet<int> GetRunningProcessIds()
        {
            HashSet<int> ids = new HashSet<int>();
            for (int i = 0; i < _count; i++) ids.Add(2000 + i);
            return ids;
        }

        public IReadOnlyList<GameProcessInfo> GetRunningGames()
        {
            List<GameProcessInfo> games = new List<GameProcessInfo>();
            for (int i = 0; i < _count; i++)
            {
                games.Add(new GameProcessInfo
                {
                    ProcessId = 2000 + i,
                    ProcessName = "onmyoji",
                    ExecutablePath = @"C:\Fake\onmyoji.exe",
                    StartTime = DateTime.Now.AddMinutes(-10)
                });
            }
            return games;
        }

        public int CountRunningGames() { return _count; }
    }

    private sealed class FakeWindowService : IGameWindowService
    {
        private readonly int _count;

        public FakeWindowService(int count) { _count = count; }

        public IReadOnlyList<GameWindowInfo> GetGameWindows()
        {
            List<GameWindowInfo> windows = new List<GameWindowInfo>();
            for (int i = 0; i < _count; i++)
            {
                windows.Add(new GameWindowInfo
                {
                    Handle = new IntPtr(3000 + i),
                    ProcessId = 2000 + i,
                    Title = i == 0 ? "阴阳师 - 网易官方版本" : "阴阳师 - 桌面版（账号 " + (i + 1) + "）",
                    Bounds = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1280, Bottom = 720 },
                    IsForeground = i == 1
                });
            }
            return windows;
        }
    }

    private sealed class FakeAudioSessionService : IAudioSessionService
    {
        public int ApplyCalls;
        public float LastVolume = -1f;
        public int LastVolumePid;

        public HashSet<int> ApplyVolumes(IDictionary<int, float> desiredVolumes)
        {
            Interlocked.Increment(ref ApplyCalls);
            HashSet<int> applied = new HashSet<int>();
            if (desiredVolumes != null)
            {
                foreach (int pid in desiredVolumes.Keys)
                {
                    applied.Add(pid);
                    if (desiredVolumes.Count == 1)
                    {
                        LastVolumePid = pid;
                        LastVolume = desiredVolumes[pid];
                    }
                }
            }
            return applied;
        }

        public Dictionary<int, float> GetVolumes(IEnumerable<int> processIds)
        {
            Dictionary<int, float> volumes = new Dictionary<int, float>();
            if (processIds != null)
                foreach (int pid in processIds) volumes[pid] = 0.8f;
            return volumes;
        }
    }

    private sealed class FakeHotkeyService : IGlobalHotkeyService
    {
        public event Action BossKeyPressed;
        public event Action<int> FocusRequested;
        public event Action<int> CycleRequested;

        public void Start() { }
        public void Stop() { }
        public void SetScheme(int scheme) { }
        public void Dispose() { }
    }

    private sealed class FakeHealthMonitor : IHealthMonitor
    {
        public event Action<IReadOnlyList<InstanceHealth>> HealthUpdated;

        public void Start() { }
        public void Stop() { }
        public void Dispose() { }

        public void Trigger(int processId, bool responsive)
        {
            Action<IReadOnlyList<InstanceHealth>> handler = HealthUpdated;
            if (handler == null) return;
            handler(new List<InstanceHealth>
            {
                new InstanceHealth { ProcessId = processId, Responsive = responsive }
            });
        }
    }

    private sealed class FakeWindowCommandService : IWindowCommandService
    {
        private int _hidden;

        public bool Focus(IntPtr handle) { return true; }
        public bool Minimize(IntPtr handle) { return true; }
        public bool Hide(IntPtr handle) { _hidden++; return true; }
        public bool Show(IntPtr handle) { if (_hidden > 0) _hidden--; return true; }
        public int HideAll(IReadOnlyList<GameWindowInfo> windows)
        {
            int count = windows == null ? 0 : windows.Count;
            _hidden += count;
            return count;
        }
        public int ShowAll()
        {
            int count = _hidden;
            _hidden = 0;
            return count;
        }
        public int HiddenCount { get { return _hidden; } }
    }

    private sealed class FakeLauncher : IGameLauncher
    {
        public async Task<LaunchResult> LaunchAsync(int target, string gameRoot, AppLogger logger, CancellationToken cancellationToken)
        {
            logger.Log("[模拟] 开始多开 " + target + " 个实例。");
            await Task.Delay(250, cancellationToken);
            return new LaunchResult { Target = target, RunningBefore = 3, Started = 1, RunningAfter = 4 };
        }
    }

    private sealed class FakeMonitorService : IMonitorService
    {
        private readonly List<MonitorInfo> _monitors;

        public FakeMonitorService()
        {
            _monitors = new List<MonitorInfo>();
            _monitors.Add(new MonitorInfo
            {
                Index = 0,
                IsPrimary = true,
                MonitorArea = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
                WorkArea = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 }
            });
        }

        public IReadOnlyList<MonitorInfo> GetMonitors() { return _monitors; }

        public NativeMethods.RECT GetWorkArea(int index)
        {
            if (index < 0 || index >= _monitors.Count) index = 0;
            return _monitors[index].WorkArea;
        }
    }

    private sealed class FakeWindowWatchService : IWindowWatchService
    {
        public event Action<IReadOnlyList<GameWindowInfo>, bool> WindowsChanged;

        public void Start() { }
        public void Stop() { }
        public void Dispose() { }

        public void Trigger(IReadOnlyList<GameWindowInfo> windows, bool added)
        {
            Action<IReadOnlyList<GameWindowInfo>, bool> handler = WindowsChanged;
            if (handler != null) handler(windows, added);
        }
    }

    private sealed class FakeArranger : IWindowArranger
    {
        public int ArrangeCalls;

        public async Task<ArrangeResult> ArrangeAsync(int columns, int monitorIndex, AppLogger logger, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ArrangeCalls);
            logger.Log("[模拟] 按每行 " + columns + " 个排列窗口，目标显示器 " + (monitorIndex + 1) + "。");
            await Task.Delay(150, cancellationToken);
            return new ArrangeResult { WindowCount = 3, Columns = columns, MonitorIndex = monitorIndex, Moved = 3 };
        }
    }

    private sealed class FakeCloser : IGameShutdownService
    {
        public async Task<CloseResult> CloseAllAsync(AppLogger logger, CancellationToken cancellationToken)
        {
            logger.Log("[模拟] 关闭所有游戏窗口。");
            await Task.Delay(150, cancellationToken);
            return new CloseResult { Total = 3, Graceful = 3 };
        }

        public async Task<CloseResult> CloseAsync(int processId, AppLogger logger, CancellationToken cancellationToken)
        {
            logger.Log("[模拟] 关闭实例 " + processId + "。");
            await Task.Delay(100, cancellationToken);
            return new CloseResult { Total = 1, Graceful = 1 };
        }
    }
}

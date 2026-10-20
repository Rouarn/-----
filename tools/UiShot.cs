using System;
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

        int instanceCount = tag == "list" ? 12 : (tag == "smoke" ? 3 : 0);
        AppSettings settings = new AppSettings
        {
            GameRoot = @"D:\Netease\yys",
            TargetInstances = 2,
            Columns = 2,
            SoundFocusEnabled = true
        };
        SettingsStore store = new SettingsStore(Path.Combine(outDir, "config.ini"));
        AppLogger logger = new AppLogger();
        FakeProcessService processes = new FakeProcessService(instanceCount);
        FakeWindowService windows = new FakeWindowService(instanceCount);
        FakeAudioSessionService audio = new FakeAudioSessionService();
        SoundFocusService sound = new SoundFocusService(processes, audio, logger);
        AppController controller = new AppController(
            settings, store, processes, windows,
            new FakeLauncher(), new FakeArranger(), new FakeCloser(),
            sound, logger);

        MainWindow window = new MainWindow(controller, logger);
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
                else if (tag == "smoke")
                {
                    await RunSmokeAsync(window, controller, audio, outDir);
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

    private static async Task RunSmokeAsync(MainWindow window, AppController controller, FakeAudioSessionService audio, string outDir)
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

            check("窗口内容已构建", window.Content is FrameworkElement);
            check("启动日志已写入", log.Text.Contains("控制台已就绪"));
            check("游戏目录已显示", root.Text.Length > 0);
            check("实例数量已刷新", winCount.Text.Contains("3 个窗口"));
            check("实例行已生成", ((Panel)Field(window, "_instancePanel")).Children.Count == 3);

            Invoke(window, "StepOpen", 1);
            check("步进器 +", openNum.Text == "3");
            Invoke(window, "StepOpen", -1);
            check("步进器 -", openNum.Text == "2");

            Invoke(window, "SetArrangeCols", 4, true);
            check("列数切换并持久化", controller.Settings.Columns == 4);
            Invoke(window, "SetArrangeCols", 2, true);
            check("列数恢复", controller.Settings.Columns == 2);

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

            Task<LaunchResult> cancelTask = controller.LaunchAsync(3);
            await Task.Delay(60);
            controller.CancelCurrentOperation();
            LaunchResult canceled = await cancelTask;
            check("后台操作可取消", canceled.Canceled && !controller.IsBusy);
            check("取消后忙状态复位", busyPanel.Visibility == Visibility.Collapsed);

            ArrangeResult arrange = await controller.ArrangeAsync(2);
            check("排列编排完成", arrange != null);
            CloseResult close = await controller.CloseAllAsync();
            check("关闭编排完成", close != null);

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

        public HashSet<int> ApplyVolumes(IDictionary<int, float> desiredVolumes)
        {
            Interlocked.Increment(ref ApplyCalls);
            HashSet<int> applied = new HashSet<int>();
            if (desiredVolumes != null)
                foreach (int pid in desiredVolumes.Keys) applied.Add(pid);
            return applied;
        }
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

    private sealed class FakeArranger : IWindowArranger
    {
        public async Task<ArrangeResult> ArrangeAsync(int columns, AppLogger logger, CancellationToken cancellationToken)
        {
            logger.Log("[模拟] 按每行 " + columns + " 个排列窗口。");
            await Task.Delay(150, cancellationToken);
            return new ArrangeResult { WindowCount = 3, Columns = columns, Moved = 3 };
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
    }
}

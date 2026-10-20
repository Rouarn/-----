using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OnmyojiTools.Core;
using OnmyojiTools.Ui;

/// <summary>开发辅助：离屏截取主窗口，便于 UI 回归检查（不参与正式构建）</summary>
internal static class UiShot
{
    [STAThread]
    private static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : ".";
        string tag = args.Length > 1 ? args[1] : "ui";
        Directory.CreateDirectory(outDir);
        try { OnmyojiTools.Interop.Native.SetProcessDpiAwareness(2); } catch { }
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        Application app = new Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        MainWindow win = new MainWindow();
        win.Show();

        win.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate
        {
            win.UpdateRunningLabel();
            win.RefreshList();
            if (tag == "smoke")
            {
                RunSmoke(win, outDir);
                app.Shutdown();
                return;
            }
            if (tag == "list") FillDemoInstances(win);
            if (tag == "about") win.ShowAbout();

            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            timer.Tick += delegate
            {
                timer.Stop();
                try
                {
                    win.UpdateLayout();
                    FrameworkElement root = win.Content as FrameworkElement;
                    if (root == null) throw new InvalidOperationException("no content");
                    Save(root, Path.Combine(outDir, tag + ".png"));
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Path.Combine(outDir, tag + "-error.txt"), ex.ToString());
                }
                app.Shutdown();
            };
            timer.Start();
        }));

        app.Run();
    }

    private static void RunSmoke(MainWindow win, string outDir)
    {
        StringBuilder sb = new StringBuilder();
        int passed = 0, failed = 0;
        Action<string, bool> check = delegate(string name, bool ok)
        {
            sb.AppendLine((ok ? "PASS  " : "FAIL  ") + name);
            if (ok) passed++; else failed++;
        };

        try
        {
            check("window content built", win.Content is FrameworkElement);
            TextBox log = (TextBox)Field(win, "txtLog");
            check("welcome log written", log != null && log.Text.Contains("控制台已就绪"));
            check("game root shown", ((TextBox)Field(win, "txtRoot")).Text.Length > 0);
            check("instance panel exists", Field(win, "_instancePanel") != null);

            TextBox openNum = (TextBox)Field(win, "txtOpenNum");
            Invoke(win, "StepOpen", 1);
            check("stepper increments", openNum.Text == "3");
            Invoke(win, "StepOpen", -1);
            check("stepper decrements", openNum.Text == "2");

            Invoke(win, "SetArrangeCols", 4);
            check("columns selection applies", (int)Field(win, "_arrangeCols") == 4);
            Invoke(win, "SetArrangeCols", 2);

            Invoke(win, "TogglePin");
            bool pinned = win.Topmost;
            Invoke(win, "TogglePin");
            check("pin toggles both ways", pinned && !win.Topmost);

            SwitchControl sw = (SwitchControl)Field(win, "swSound");
            sw.IsOn = false;
            TextBlock state = (TextBlock)Field(win, "lblSoundState");
            check("sound switch off state", state.Text == "已暂停");
            sw.IsOn = true;
            check("sound switch on state", state.Text == "监控中");

            Invoke(win, "ShowAbout");
            Grid overlay = (Grid)Field(win, "_aboutOverlay");
            check("about overlay opens", overlay.Visibility == Visibility.Visible);
            Invoke(win, "CloseAbout");
            check("about overlay closes", overlay.Visibility == Visibility.Collapsed);

            log.Clear();
            Invoke(win, "Log", "smoke");
            check("log append after clear", log.Text.Contains("smoke"));

            Invoke(win, "RefreshList");
            check("refresh list runs", ((TextBlock)Field(win, "lblScan")).Text.Contains("最后刷新"));
        }
        catch (Exception ex)
        {
            failed++;
            sb.AppendLine("EXCEPTION  " + ex);
        }

        sb.AppendLine("RESULT passed=" + passed + " failed=" + failed);
        File.WriteAllText(Path.Combine(outDir, "smoke.txt"), sb.ToString(), Encoding.UTF8);
    }

    private static object Field(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }

    private static void Invoke(object target, string name, params object[] args)
    {
        MethodInfo m = target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
        if (m == null) throw new MissingMethodException(name);
        m.Invoke(target, args);
    }

    private static void FillDemoInstances(MainWindow win)
    {
        const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
        MethodInfo build = typeof(MainWindow).GetMethod("BuildInstanceRow", F);
        StackPanel panel = (StackPanel)typeof(MainWindow).GetField("_instancePanel", F).GetValue(win);
        ScrollViewer scroll = (ScrollViewer)typeof(MainWindow).GetField("_instanceScroll", F).GetValue(win);
        Border empty = (Border)typeof(MainWindow).GetField("_emptyState", F).GetValue(win);
        TextBlock count = (TextBlock)typeof(MainWindow).GetField("lblWinCount", F).GetValue(win);
        TextBlock scan = (TextBlock)typeof(MainWindow).GetField("lblScan", F).GetValue(win);

        string[] titles =
        {
            "阴阳师 - 网易官方版本",
            "阴阳师 - 桌面版（账号 2）",
            "阴阳师 - 桌面版（账号 3）",
            "阴阳师 - 桌面版（账号 4）",
            "阴阳师 - 桌面版（账号 5）",
            "阴阳师 - 桌面版（账号 6）",
            "阴阳师 - 桌面版（账号 7）",
            "阴阳师 - 桌面版（账号 8）",
            "阴阳师 - 桌面版（账号 9）",
            "阴阳师 - 桌面版（账号 10）",
            "阴阳师 - 桌面版（账号 11）",
            "阴阳师 - 桌面版（账号 12）"
        };

        panel.Children.Clear();
        for (int i = 0; i < titles.Length; i++)
        {
            YysWindow w = new YysWindow
            {
                Hwnd = IntPtr.Zero,
                Pid = (uint)(12640 + i * 4),
                Title = titles[i],
                Focused = i == 1
            };
            panel.Children.Add((Border)build.Invoke(win, new object[] { i + 1, w }));
        }
        scroll.Visibility = Visibility.Visible;
        empty.Visibility = Visibility.Collapsed;
        count.Text = titles.Length + " 个窗口";
        scan.Text = "共 " + titles.Length + " 个 · 最后刷新 12:34:56";

        TextBlock running = (TextBlock)typeof(MainWindow).GetField("lblRunning", F).GetValue(win);
        System.Windows.Shapes.Ellipse dot = (System.Windows.Shapes.Ellipse)typeof(MainWindow).GetField("_statusDot", F).GetValue(win);
        running.Text = titles.Length.ToString();
        dot.Fill = Theme.B(Theme.Green);

        TextBox log = (TextBox)typeof(MainWindow).GetField("txtLog", F).GetValue(win);
        log.Clear();
        Invoke(win, "Log", "控制台已就绪。");
        Invoke(win, "Log", "游戏目录：D:\\Netease\\yys");
        Invoke(win, "Log", "识别到阴阳师窗口 " + titles.Length + " 个");
        Invoke(win, "Log", "声音跟随聚焦已开启：聚焦窗口正常音量，失焦窗口自动调小。");
    }

    private static void Save(FrameworkElement root, string path)
    {
        root.UpdateLayout();
        int w = (int)Math.Ceiling(root.ActualWidth);
        int h = (int)Math.Ceiling(root.ActualHeight);
        if (w < 2 || h < 2) throw new InvalidOperationException("empty layout " + w + "x" + h);
        RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(root);
        PngBitmapEncoder enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using (FileStream fs = File.Create(path)) enc.Save(fs);
    }
}

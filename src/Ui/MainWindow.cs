using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using OnmyojiTools.Audio;
using OnmyojiTools.Core;
using Path = System.IO.Path;

namespace OnmyojiTools.Ui
{
    /// <summary>主窗口：和纸 · 墨 · 朱印风格的单屏控制台</summary>
    public partial class MainWindow : Window
    {
        // ---------- 控件 ----------
        private TextBox txtRoot;
        private TextBox txtOpenNum;
        private TextBox txtLog;
        private TextBlock lblRunning;
        private TextBlock lblWinCount;
        private TextBlock lblScan;
        private TextBlock lblSoundState;
        private TextBlock lblAdmin;
        private Ellipse _statusDot;
        private Ellipse _soundDot;
        private Button btnOpen, btnArrange, btnRefresh, btnCloseAll, btnBrowse, btnPin;
        private StackPanel _instancePanel;
        private ScrollViewer _instanceScroll;
        private Border _emptyState;
        private SwitchControl swSound;
        private DispatcherTimer timerSound;
        private SoundFocusController _sf;
        private Grid _aboutOverlay;
        private Border _shell;
        private Border[] _colSegments = new Border[4];
        private TextBlock[] _colLabels = new TextBlock[4];
        private int _arrangeCols = 2;
        private readonly AppConfig _config = new AppConfig();
        private readonly bool _isAdmin = IsAdministrator();

        public MainWindow()
        {
            Title = "阴阳师工具 · 桌面控制台";
            Width = 1120;
            Height = 760;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = Theme.FontUI;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            Resources.MergedDictionaries.Add(Theme.CommonResources());

            BuildUi();
            _config.Load();
            txtRoot.Text = _config.YysRoot;

            // 声音跟随聚焦：常驻监控
            AudioApi.LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio_log.txt");
            _sf = new SoundFocusController();
            timerSound = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timerSound.Tick += TimerSound_Tick;
            Closed += delegate { timerSound.Stop(); };
            if (swSound != null && swSound.IsOn) timerSound.Start();

            Log("控制台已就绪。");
            Log("游戏目录：" + _config.YysRoot);
            if (!_isAdmin) Log("提示：当前为普通权限，移动游戏窗口或操作音量可能失败，建议以管理员身份运行。");

            // 启动即激活前台，避免无边框窗口首次点击只激活不触发按钮
            Loaded += delegate
            {
                Activate();
                Topmost = false;
                if (_shell != null)
                {
                    DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
                    fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                    _shell.BeginAnimation(UIElement.OpacityProperty, fade);
                }
            };

            PreviewKeyDown += MainWindow_PreviewKeyDown;
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                WindowsPrincipal p = new WindowsPrincipal(id);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        // ==================== 界面构建 ====================
        private void BuildUi()
        {
            _shell = new Border
            {
                Background = Theme.PaperBackground(),
                BorderBrush = Theme.B(Theme.LineStrong),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                ClipToBounds = true,
                SnapsToDevicePixels = true,
                Opacity = 0
            };

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });

            // ---- 顶栏 ----
            UIElement header = BuildHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ---- 主体 ----
            Grid body = new Grid { Margin = new Thickness(22, 18, 22, 14) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(356) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel left = new StackPanel();
            Border cardLaunch = BuildLaunchCard();
            cardLaunch.Margin = new Thickness(0, 0, 0, 14);
            left.Children.Add(cardLaunch);
            Border cardArrange = BuildArrangeCard();
            cardArrange.Margin = new Thickness(0, 0, 0, 14);
            left.Children.Add(cardArrange);
            left.Children.Add(BuildSoundCard());
            Grid.SetColumn(left, 0);
            body.Children.Add(left);

            Grid right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(216) });
            Border cardInstances = BuildInstancesCard();
            Grid.SetRow(cardInstances, 0);
            right.Children.Add(cardInstances);
            Border cardLog = BuildLogCard();
            Grid.SetRow(cardLog, 2);
            right.Children.Add(cardLog);
            Grid.SetColumn(right, 2);
            body.Children.Add(right);

            Grid.SetRow(body, 1);
            root.Children.Add(body);

            // ---- 底栏 ----
            UIElement footer = BuildFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            // ---- 关于弹层 ----
            _aboutOverlay = BuildAboutOverlay();
            Grid.SetRowSpan(_aboutOverlay, 3);
            root.Children.Add(_aboutOverlay);

            _shell.Child = root;
            Content = _shell;
        }

        private UIElement BuildHeader()
        {
            Border bar = new Border
            {
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(22, 0, 16, 0),
                Background = Brushes.Transparent
            };
            bar.MouseLeftButtonDown += TitleBar_MouseDown;

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 品牌：朱印 + 名称 + 英文小标
            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            brand.Children.Add(Theme.Seal(30));
            StackPanel brandText = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            brandText.Children.Add(new TextBlock
            {
                Text = "阴阳师工具",
                FontFamily = Theme.FontUI,
                FontSize = 15.5,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.B(Theme.Ink)
            });
            StackPanel sub = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            sub.Children.Add(new TextBlock
            {
                Text = "ONMYOJI CONSOLE",
                FontFamily = Theme.FontSerif,
                FontSize = 8.5,
                Foreground = Theme.B(Theme.TextMuted)
            });
            sub.Children.Add(new Border
            {
                Width = 14,
                Height = 1,
                Background = Theme.B(Theme.LineStrong),
                Margin = new Thickness(7, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            sub.Children.Add(new TextBlock
            {
                Text = "v3.0",
                FontFamily = Theme.FontSerif,
                FontSize = 8.6,
                Foreground = Theme.B(Theme.Gold)
            });
            brandText.Children.Add(sub);
            brand.Children.Add(brandText);
            Grid.SetColumn(brand, 0);
            g.Children.Add(brand);

            // 运行状态模块
            _statusDot = Theme.Dot(6, Theme.TextMuted);
            lblRunning = new TextBlock
            {
                Text = "0",
                FontFamily = Theme.FontSerif,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.B(Theme.Ink),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(7, 0, 4, 0)
            };
            StackPanel status = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            status.Children.Add(_statusDot);
            status.Children.Add(new TextBlock
            {
                Text = "运行实例",
                FontFamily = Theme.FontUI,
                FontSize = 10.2,
                Foreground = Theme.B(Theme.TextSub),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(7, 0, 0, 0)
            });
            status.Children.Add(lblRunning);
            status.Children.Add(new TextBlock
            {
                Text = "个",
                FontFamily = Theme.FontUI,
                FontSize = 10.2,
                Foreground = Theme.B(Theme.TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            });
            Border statusBox = new Border
            {
                Background = Theme.B(Theme.SurfaceSoft),
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(10, 4, 11, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0),
                Child = status,
                ToolTip = "当前正在运行的阴阳师进程数"
            };
            Grid.SetColumn(statusBox, 2);
            g.Children.Add(statusBox);

            // 分隔 + 窗口按钮
            Border sep = new Border
            {
                Width = 1,
                Height = 18,
                Background = Theme.B(Theme.Line),
                Margin = new Thickness(14, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sep, 3);
            g.Children.Add(sep);

            StackPanel chrome = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            btnPin = Theme.MakeChromeButton("\uE840", false, TogglePin, "窗口置顶：始终显示在最前");
            btnPin.Margin = new Thickness(0, 0, 4, 0);
            chrome.Children.Add(btnPin);
            Button btnMin = Theme.MakeChromeButton("\uE921", false, delegate { WindowState = WindowState.Minimized; }, "最小化");
            chrome.Children.Add(btnMin);
            Button btnClose = Theme.MakeChromeButton("\uE8BB", true, delegate { Close(); }, "关闭");
            chrome.Children.Add(btnClose);
            Grid.SetColumn(chrome, 5);
            g.Children.Add(chrome);

            bar.Child = g;
            return bar;
        }

        private UIElement BuildFooter()
        {
            Border bar = new Border
            {
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(22, 0, 22, 0)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(new TextBlock
            {
                Text = "v3.0 · 朱印界面",
                FontFamily = Theme.FontUI,
                FontSize = 9.8,
                Foreground = Theme.B(Theme.TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            });
            left.Children.Add(new Border
            {
                Width = 1,
                Height = 9,
                Background = Theme.B(Theme.LineStrong),
                Margin = new Thickness(9, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            left.Children.Add(new TextBlock
            {
                Text = "Windows 10 / 11 · .NET Framework 4.x",
                FontFamily = Theme.FontUI,
                FontSize = 9.8,
                Foreground = Theme.B(Theme.TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(left, 0);
            g.Children.Add(left);

            StackPanel right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Ellipse adminDot = Theme.Dot(5, _isAdmin ? Theme.Green : Theme.Amber);
            right.Children.Add(adminDot);
            lblAdmin = new TextBlock
            {
                Text = _isAdmin ? "管理员权限" : "普通权限",
                FontFamily = Theme.FontUI,
                FontSize = 9.8,
                Foreground = Theme.B(_isAdmin ? Theme.Green : Theme.Amber),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                ToolTip = _isAdmin ? "已具备窗口与音量控制所需权限" : "部分窗口操作可能失败，建议右键以管理员身份运行"
            };
            right.Children.Add(lblAdmin);

            Border sep2 = new Border
            {
                Width = 1,
                Height = 9,
                Background = Theme.B(Theme.LineStrong),
                Margin = new Thickness(12, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            right.Children.Add(sep2);

            Button btnDoc = Theme.MakeButton("使用说明", OpenUsageDoc, ButtonKind.Link, 22);
            btnDoc.Padding = new Thickness(0);
            btnDoc.Margin = new Thickness(0, 0, 4, 0);
            right.Children.Add(btnDoc);
            Button btnAbout = Theme.MakeButton("关于", ShowAbout, ButtonKind.Link, 22);
            btnAbout.Padding = new Thickness(0);
            right.Children.Add(btnAbout);

            Grid.SetColumn(right, 1);
            g.Children.Add(right);

            bar.Child = g;
            return bar;
        }

        // ---------- 标题栏交互 ----------
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 点击交互控件时不触发拖拽（DragMove 会吞掉按钮 Click）
            DependencyObject src = e.OriginalSource as DependencyObject;
            while (src != null)
            {
                if (src is ButtonBase || src is SwitchControl || src is TextBox) return;
                src = VisualTreeHelper.GetParent(src);
            }
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _aboutOverlay != null && _aboutOverlay.Visibility == Visibility.Visible)
            {
                CloseAbout();
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                UpdateRunningLabel();
                RefreshList();
                e.Handled = true;
            }
        }

        // ---------- 置顶 ----------
        private void TogglePin()
        {
            Topmost = !Topmost;
            if (Topmost)
            {
                btnPin.Content = "\uE841";
                btnPin.Background = Theme.B(Theme.AccentSoft);
                btnPin.BorderBrush = Theme.B(Theme.AccentLine);
                btnPin.Foreground = Theme.B(Theme.Accent);
                btnPin.ToolTip = "取消置顶";
            }
            else
            {
                btnPin.Content = "\uE840";
                btnPin.Background = Brushes.Transparent;
                btnPin.BorderBrush = Brushes.Transparent;
                btnPin.Foreground = Theme.B(Theme.TextSub);
                btnPin.ToolTip = "窗口置顶：始终显示在最前";
            }
            Log("窗口置顶已" + (Topmost ? "开启" : "关闭") + "。");
        }

        // ---------- 日志 ----------
        private void Log(string s)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string>(Log), s);
                return;
            }
            if (txtLog == null) return;
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + Environment.NewLine);
            txtLog.ScrollToEnd();
        }

        // ==================== 声音跟随聚焦 ====================
        private void ChkSound_Changed(bool on)
        {
            if (timerSound == null) return;
            if (on)
            {
                timerSound.Start();
                Log("声音跟随聚焦已开启：聚焦窗口正常音量，失焦窗口自动调小。");
            }
            else
            {
                timerSound.Stop();
                Log("声音跟随聚焦已关闭。");
            }
            if (lblSoundState != null)
            {
                lblSoundState.Text = on ? "监控中" : "已暂停";
                lblSoundState.Foreground = Theme.B(on ? Theme.Green : Theme.TextMuted);
            }
            if (_soundDot != null) _soundDot.Fill = Theme.B(on ? Theme.Green : Theme.TextMuted);
        }

        private int _scanning;

        private void TimerSound_Tick(object sender, EventArgs e)
        {
            // 声音扫描较重（WASAPI 枚举 + 日志），必须移出 UI 线程，否则每 500ms 卡住界面导致点击无反应
            if (Interlocked.Exchange(ref _scanning, 1) == 1) return;
            try
            {
                Task.Run(delegate
                {
                    try { _sf.Scan(); }
                    finally { Interlocked.Exchange(ref _scanning, 0); }
                });
            }
            catch { Interlocked.Exchange(ref _scanning, 0); }
        }

        // ==================== 多开 ====================
        private async Task BtnOpen_Click()
        {
            Log("=== 开始多开 ===");
            int target;
            if (!ReadNum(txtOpenNum, 1, 10, out target)) return;
            btnOpen.IsEnabled = false;
            txtOpenNum.IsEnabled = false;
            try
            {
                await Task.Run(delegate
                {
                    if (!_config.Validate(Log)) return;
                    GameLauncher.Launch(target, _config.YysRoot, Log);
                });
            }
            finally
            {
                btnOpen.IsEnabled = true;
                txtOpenNum.IsEnabled = true;
                UpdateRunningLabel();
                RefreshList();
            }
        }

        private bool ReadNum(TextBox box, int min, int max, out int val)
        {
            if (int.TryParse(box.Text.Trim(), out val) && val >= min && val <= max) return true;
            Log(string.Format("输入无效：请输入 {0}-{1} 之间的整数（当前输入：{2}）。", min, max, box.Text));
            box.Focus();
            box.SelectAll();
            return false;
        }

        internal void UpdateRunningLabel()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(UpdateRunningLabel));
                return;
            }
            int n = GameLauncher.CountRunning();
            if (lblRunning != null) lblRunning.Text = n.ToString();
            if (_statusDot != null) _statusDot.Fill = Theme.B(n > 0 ? Theme.Green : Theme.TextMuted);
        }

        // ==================== 一键关闭所有游戏窗口 ====================
        private async Task BtnCloseAll_Click()
        {
            Log("=== 一键关闭所有游戏 ===");
            btnCloseAll.IsEnabled = false;
            try
            {
                await Task.Run(delegate { GameCloser.CloseAll(Log); });
            }
            finally
            {
                btnCloseAll.IsEnabled = true;
            }
            await Task.Delay(400);
            UpdateRunningLabel();
            RefreshList();
        }

        // ==================== 排列（探测-定位两阶段） ====================
        private async Task BtnArrange_Click()
        {
            Log("=== 开始排列窗口（每行 " + _arrangeCols + " 个）===");
            Mouse.OverrideCursor = Cursors.Wait;
            btnArrange.IsEnabled = false;
            try
            {
                await Task.Run(delegate { WindowArranger.Arrange(_arrangeCols, Log); });
            }
            finally
            {
                btnArrange.IsEnabled = true;
                Mouse.OverrideCursor = null;
            }
            UpdateRunningLabel();
            RefreshList();
        }

        internal void RefreshList()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RefreshList));
                return;
            }
            List<YysWindow> wins = WindowFinder.EnumerateYys();
            _instancePanel.Children.Clear();
            for (int i = 0; i < wins.Count; i++)
                _instancePanel.Children.Add(BuildInstanceRow(i + 1, wins[i]));

            bool empty = wins.Count == 0;
            _emptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            _instanceScroll.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            lblWinCount.Text = wins.Count + " 个窗口";
            lblScan.Text = "共 " + wins.Count + " 个 · 最后刷新 " + DateTime.Now.ToString("HH:mm:ss");
            Log("识别到阴阳师窗口 " + wins.Count + " 个");
        }

        private void BtnRefresh_Click()
        {
            UpdateRunningLabel();
            RefreshList();
        }

        private void BtnBrowse_Click()
        {
            using (System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "选择阴阳师安装根目录（含 bin\\onmyoji.exe 的目录）";
                dlg.SelectedPath = Directory.Exists(_config.YysRoot) ? _config.YysRoot : AppConfig.DefaultRoot;
                dlg.ShowNewFolderButton = false;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string dir = dlg.SelectedPath;
                    if (!File.Exists(Path.Combine(dir, "bin", "onmyoji.exe")))
                    {
                        Log("所选目录未找到 bin\\onmyoji.exe，请重新选择：" + dir);
                        return;
                    }
                    _config.YysRoot = dir;
                    txtRoot.Text = dir;
                    string err = _config.Save();
                    if (err != null) Log("保存配置失败：" + err);
                    Log("工作目录已设置为：" + dir + "（已保存，下次启动自动加载）");
                }
            }
        }

        private void OpenUsageDoc()
        {
            string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "使用说明.txt");
            if (!File.Exists(p))
            {
                Log("未找到「使用说明.txt」。");
                return;
            }
            try { Process.Start("notepad.exe", "\"" + p + "\""); }
            catch (Exception ex) { Log("打开使用说明失败：" + ex.Message); }
        }

        // ==================== 关于弹层 ====================
        internal void ShowAbout()
        {
            if (_aboutOverlay == null) return;
            _aboutOverlay.Visibility = Visibility.Visible;
            _aboutOverlay.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        }

        private void CloseAbout()
        {
            if (_aboutOverlay == null) return;
            _aboutOverlay.Visibility = Visibility.Collapsed;
        }
    }
}

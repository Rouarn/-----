using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using OnmyojiTools.Audio;
using OnmyojiTools.Core;

namespace OnmyojiTools.Ui
{
    /// <summary>主窗口（WPF 现代化深色卡片式界面）</summary>
    public partial class MainWindow : Window
    {
        // ---------- 控件 ----------
        private TextBox txtRoot;
        private TextBox txtOpenNum;
        private TextBox txtColsNum;
        private TextBlock lblRunning;
        private TextBlock lblWinCount;
        private Button btnOpen, btnArrange, btnRefresh, btnCloseAll, btnBrowse;
        private Button btnPin;
        private ListView listWins;
        private TextBox txtLog;
        private DispatcherTimer timerSound;
        private SoundFocusController _sf;
        private Grid pageOpen, pageArrange, pageSound, pageAbout;
        private Border navOpen, navArrange, navSound, navAbout;
        private SwitchControl swSound;
        private readonly AppConfig _config = new AppConfig();

        public MainWindow()
        {
            Title = "阴阳师工具 - 多开 & 窗口排列";
            Width = 1080;
            Height = 760;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = Theme.FontYH;

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

            // 启动即激活前台，避免无边框窗口首次点击只激活不触发按钮
            Loaded += (s, e) =>
            {
                Activate();
                Topmost = false;
            };
        }

        // ==================== 界面构建 ====================
        private void BuildUi()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 根容器（圆角 + 阴影 + 微渐变）
            Border shell = new Border
            {
                CornerRadius = new CornerRadius(16),
                ClipToBounds = true,
                Effect = new DropShadowEffect { BlurRadius = 32, ShadowDepth = 10, Opacity = 0.55, Color = Colors.Black }
            };
            LinearGradientBrush bg = new LinearGradientBrush(
                Color.FromRgb(0x10, 0x22, 0x2C), Theme.BgMain, 90d);
            bg.Freeze();
            shell.Background = bg;
            Content = shell;

            // ==================== 标题栏 ====================
            Grid bar = new Grid();
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.MouseLeftButtonDown += TitleBar_MouseDown;

            // 左侧：图标 + 标题
            StackPanel titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 0, 0) };
            Border iconBox = new Border
            {
                Width = 30, Height = 30,
                CornerRadius = new CornerRadius(9),
                Background = Theme.AccentGradient(),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            iconBox.Child = new TextBlock
            {
                Text = "\uE7FC",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleSp.Children.Add(iconBox);
            titleSp.Children.Add(new TextBlock
            {
                Text = "阴阳师工具",
                FontFamily = Theme.FontYH,
                FontSize = 16.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.BrushFrozen(Theme.TextMain),
                VerticalAlignment = VerticalAlignment.Center
            });
            titleSp.Children.Add(new TextBlock
            {
                Text = "  多开 · 窗口排列 · 声随聚焦",
                FontFamily = Theme.FontYH,
                FontSize = 11.5,
                Foreground = Theme.BrushFrozen(Theme.TextSub),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 2, 0, 0)
            });
            Grid.SetColumn(titleSp, 0);
            bar.Children.Add(titleSp);

            // 运行状态徽标
            lblRunning = Theme.Label("当前运行实例：--", 11.5, true, Theme.BrushFrozen(Theme.Success));
            Border runBadge = new Border
            {
                Background = Theme.BrushFrozen(Color.FromRgb(0x1B, 0x2E, 0x26)),
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(14, 5, 14, 5),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Child = lblRunning
            };
            Grid.SetColumn(runBadge, 1);
            bar.Children.Add(runBadge);

            // 置顶开关（防被其它窗口遮挡导致点击失效）
            btnPin = new Button
            {
                Content = "\uE840", // Segoe MDL2 Assets: Pin（图钉）
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Width = 46,
                Height = 34,
                Foreground = Theme.BrushFrozen(Theme.TextSub),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = Theme.BrushFrozen(Theme.BorderLine),
                Cursor = Cursors.Hand,
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 2, 0)
            };
            btnPin.Template = TitleButtonTextTemplate();
            btnPin.ToolTip = "窗口置顶：开启后工具窗口始终显示在最前，点击不会被其它窗口遮挡";
            btnPin.Click += (s, e) => TogglePin();
            Grid.SetColumn(btnPin, 2);
            bar.Children.Add(btnPin);

            Button btnMin = TitleButton("\uE921", false);
            btnMin.Margin = new Thickness(0, 0, 2, 0);
            btnMin.Click += (s, e) => WindowState = WindowState.Minimized;
            Grid.SetColumn(btnMin, 3);
            bar.Children.Add(btnMin);

            Button btnClose = TitleButton("\uE8BB", true);
            btnClose.Margin = new Thickness(0, 0, 12, 0);
            btnClose.Click += (s, e) => Close();
            Grid.SetColumn(btnClose, 4);
            bar.Children.Add(btnClose);

            Grid.SetRow(bar, 0);
            root.Children.Add(bar);

            // 标题栏分隔线
            Border sep = new Border
            {
                Height = 1,
                Background = Theme.BrushFrozen(Color.FromRgb(0x22, 0x28, 0x34)),
                Margin = new Thickness(20, 0, 20, 0),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetRow(sep, 0);
            root.Children.Add(sep);

            // ==================== 主体：左导航 + 右内容 ====================
            Grid body = new Grid { Margin = new Thickness(18, 10, 18, 16) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(196) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ---- 左侧导航 ----
            StackPanel nav = new StackPanel { Margin = new Thickness(4, 6, 0, 0) };
            TextBlock navTitle = Theme.Label("功能导航", 10.5);
            navTitle.Margin = new Thickness(14, 0, 0, 10);
            nav.Children.Add(navTitle);

            navOpen = NavItem("\uE8C8", "多开管理", true, () => SwitchNav(navOpen));
            nav.Children.Add(navOpen);
            navArrange = NavItem("\uE9E9", "窗口排列", false, () => SwitchNav(navArrange));
            nav.Children.Add(navArrange);
            navSound = NavItem("\uE767", "声音控制", false, () => SwitchNav(navSound));
            nav.Children.Add(navSound);
            navAbout = NavItem("\uE946", "关于", false, () => SwitchNav(navAbout));
            nav.Children.Add(navAbout);

            Grid.SetColumn(nav, 0);
            body.Children.Add(nav);

            // 导航分隔线
            Border navSep = new Border
            {
                Width = 1,
                Background = Theme.BrushFrozen(Color.FromRgb(0x22, 0x28, 0x34)),
                Margin = new Thickness(0, 2, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(navSep, 0);
            body.Children.Add(navSep);

            // ---- 右侧：页面 + 日志 ----
            Grid right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(172) });

            // 页面容器（4 页切换）
            Grid pageHost = new Grid();
            pageOpen = BuildPageOpen();
            pageArrange = BuildPageArrange();
            pageSound = BuildPageSound();
            pageAbout = BuildPageAbout();
            pageHost.Children.Add(pageOpen);
            pageHost.Children.Add(pageArrange);
            pageHost.Children.Add(pageSound);
            pageHost.Children.Add(pageAbout);
            pageArrange.Visibility = Visibility.Collapsed;
            pageSound.Visibility = Visibility.Collapsed;
            pageAbout.Visibility = Visibility.Collapsed;
            Grid.SetRow(pageHost, 0);
            right.Children.Add(pageHost);

            // 日志卡片
            Border cardLog = BuildLogCard();
            Grid.SetRow(cardLog, 2);
            right.Children.Add(cardLog);

            Grid.SetColumn(right, 2);
            body.Children.Add(right);

            Grid.SetRow(body, 1);
            root.Children.Add(body);

            shell.Child = root;
        }

        // ---------- 导航项 ----------
        private Border NavItem(string glyph, string text, bool selected, Action onClick)
        {
            Grid g = new Grid { Margin = new Thickness(2, 0, 6, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border indicator = new Border
            {
                Width = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = Theme.BrushFrozen(Theme.AccentLight),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = selected ? Visibility.Visible : Visibility.Collapsed
            };
            Grid.SetColumn(indicator, 0);
            g.Children.Add(indicator);

            TextBlock icon = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = Theme.BrushFrozen(selected ? Theme.AccentLight : Theme.TextSub),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 1);
            g.Children.Add(icon);

            TextBlock label = Theme.Label(text, 13, true, Theme.BrushFrozen(selected ? Theme.TextMain : Theme.TextSub));
            Grid.SetColumn(label, 2);
            g.Children.Add(label);

            Border b = new Border
            {
                Height = 44,
                CornerRadius = new CornerRadius(12),
                Background = Theme.BrushFrozen(selected ? Theme.NavSel : Color.FromArgb(0, 0, 0, 0)),
                Cursor = Cursors.Hand,
                Child = g,
                Tag = selected
            };
            b.MouseEnter += (s, e) => { if (!(bool)b.Tag) b.Background = Theme.BrushFrozen(Theme.NavHover); };
            b.MouseLeave += (s, e) => { if (!(bool)b.Tag) b.Background = Theme.BrushFrozen(Color.FromArgb(0, 0, 0, 0)); };
            if (onClick != null) b.MouseLeftButtonDown += (s, e) => onClick();
            return b;
        }

        private void SwitchNav(Border active)
        {
            Border[] navs = new[] { navOpen, navArrange, navSound, navAbout };
            Grid[] pages = new[] { pageOpen, pageArrange, pageSound, pageAbout };
            for (int i = 0; i < navs.Length; i++)
            {
                bool on = navs[i] == active;
                navs[i].Tag = on;
                navs[i].Background = Theme.BrushFrozen(on ? Theme.NavSel : Color.FromArgb(0, 0, 0, 0));
                pages[i].Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                Grid g = navs[i].Child as Grid;
                if (g != null)
                {
                    TextBlock icon = g.Children[1] as TextBlock;
                    TextBlock label = g.Children[2] as TextBlock;
                    Border indicator = g.Children[0] as Border;
                    if (icon != null) icon.Foreground = Theme.BrushFrozen(on ? Theme.AccentLight : Theme.TextSub);
                    if (label != null) label.Foreground = Theme.BrushFrozen(on ? Theme.TextMain : Theme.TextSub);
                    if (indicator != null) indicator.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private Button TitleButton(string glyph, bool danger)
        {
            Button b = new Button
            {
                Width = 46,
                Height = 34,
                Content = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Foreground = Theme.BrushFrozen(Theme.TextMain),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            ControlTemplate t = new ControlTemplate(typeof(Button));
            t.VisualTree = bd;
            Trigger hov = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(Button.BackgroundProperty,
                Theme.BrushFrozen(danger ? Color.FromRgb(0xC0, 0x3A, 0x3F) : Color.FromRgb(0x26, 0x2E, 0x3B))));
            t.Triggers.Add(hov);
            b.Template = t;
            return b;
        }

        // 置顶按钮模板（带边框 + hover 高亮）
        private ControlTemplate TitleButtonTextTemplate()
        {
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bd.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            bd.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            ControlTemplate t = new ControlTemplate(typeof(Button));
            t.VisualTree = bd;
            Trigger hov = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(Button.BackgroundProperty, Theme.BrushFrozen(Theme.BgHover)));
            hov.Setters.Add(new Setter(Button.BorderBrushProperty, Theme.BrushFrozen(Theme.Accent)));
            hov.Setters.Add(new Setter(Button.ForegroundProperty, Theme.BrushFrozen(Theme.TextMain)));
            t.Triggers.Add(hov);
            return t;
        }

        // 置顶开关：切换窗口 Topmost，并用霓虹色高亮激活态
        private void TogglePin()
        {
            Topmost = !Topmost;
            if (Topmost)
            {
                btnPin.Content = "\uE841"; // PinFill 实心图钉
                btnPin.Background = Theme.AccentGradient();
                btnPin.BorderBrush = Theme.BrushFrozen(Theme.Accent);
                btnPin.Foreground = Theme.BrushFrozen(Colors.White);
            }
            else
            {
                btnPin.Content = "\uE840"; // Pin 轮廓图钉
                btnPin.Background = Brushes.Transparent;
                btnPin.BorderBrush = Theme.BrushFrozen(Theme.BorderLine);
                btnPin.Foreground = Theme.BrushFrozen(Theme.TextSub);
            }
            Log("窗口置顶已" + (Topmost ? "开启" : "关闭") + "（避免被其它窗口遮挡导致点击无效）");
        }

        // ---------- 标题栏交互 ----------
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 点击标题栏上的按钮/开关等交互控件时不触发拖拽（否则 DragMove 会吞掉按钮 Click）
            DependencyObject src = e.OriginalSource as DependencyObject;
            while (src != null && !(src is Button) && !(src is SwitchControl))
                src = VisualTreeHelper.GetParent(src);
            if (src != null) return;
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        // ---------- 日志 ----------
        private void Log(string s)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string>(Log), s);
                return;
            }
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + Environment.NewLine);
            txtLog.ScrollToEnd();
        }

        // ==================== 声音跟随聚焦 ====================
        private void ChkSound_Changed(bool on)
        {
            if (on)
            {
                timerSound.Start();
                Log("声音跟随聚焦监控已开启：聚焦窗口正常音量，失焦窗口自动调小。");
            }
            else
            {
                timerSound.Stop();
                Log("声音跟随聚焦监控已关闭。");
            }
        }

        private int _scanning;

        private void TimerSound_Tick(object sender, EventArgs e)
        {
            // 声音扫描较重（WASAPI 枚举 + 日志），必须移出 UI 线程，否则每 500ms 卡住界面导致点击无反应
            if (Interlocked.Exchange(ref _scanning, 1) == 1) return;
            try
            {
                Task.Run(() =>
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
                await Task.Run(() =>
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
            lblRunning.Text = "当前运行实例：" + GameLauncher.CountRunning();
            lblRunning.Foreground = Theme.BrushFrozen(Theme.Success);
        }

        // ==================== 一键关闭所有游戏窗口 ====================
        private async Task BtnCloseAll_Click()
        {
            Log("=== 一键关闭所有游戏 ===");
            btnCloseAll.IsEnabled = false;
            try
            {
                await Task.Run(() => GameCloser.CloseAll(Log));
            }
            finally
            {
                btnCloseAll.IsEnabled = true;
                Thread.Sleep(500);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateRunningLabel();
                    RefreshList();
                }));
            }
        }

        // ==================== 排列（探测-定位两阶段） ====================
        private void BtnArrange_Click()
        {
            int cols;
            if (!ReadNum(txtColsNum, 1, 4, out cols)) return;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                WindowArranger.Arrange(cols, Log);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
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

        internal void RefreshList()
        {
            List<YysWindow> wins = WindowFinder.EnumerateYys();
            listWins.Items.Clear();
            foreach (YysWindow w in wins)
            {
                string mark = w.Focused ? "（聚焦）" : "";
                listWins.Items.Add(string.Format("[PID {0}]  {1}  {2}", w.Pid, w.Title, mark));
            }
            lblWinCount.Text = wins.Count + " 个";
            Log("识别到阴阳师窗口 " + wins.Count + " 个");
        }
    }
}

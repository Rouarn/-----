using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using OnmyojiTools.Core;

namespace OnmyojiTools.Ui
{
    /// <summary>主窗口 —— 卡片、列表、日志与关于弹层（partial）</summary>
    public partial class MainWindow
    {
        // ==================== 01 多开控制 ====================
        private Border BuildLaunchCard()
        {
            StackPanel sp = new StackPanel();
            sp.Children.Add(Theme.CardHeader("01", "多开控制", "按 8 秒间隔补足实例，避免客户端抢占失败", null));

            Grid rowOpen = new Grid();
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel openLabel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            openLabel.Children.Add(Theme.Text("目标开数", 12, FontWeights.SemiBold, Theme.Ink));
            openLabel.Children.Add(Theme.Meta("1 – 10 个实例"));
            Grid.SetColumn(openLabel, 0);
            rowOpen.Children.Add(openLabel);

            Border stepper = BuildOpenStepper();
            Grid.SetColumn(stepper, 1);
            rowOpen.Children.Add(stepper);
            sp.Children.Add(rowOpen);

            btnOpen = Theme.MakeButton("开始多开", delegate { RunTask(BtnOpen_Click()); }, ButtonKind.Primary, 42);
            btnOpen.Margin = new Thickness(0, 12, 0, 0);
            sp.Children.Add(btnOpen);

            sp.Children.Add(Theme.Divider(14, 10));
            sp.Children.Add(Theme.Meta("游戏工作目录"));
            Grid rowRoot = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            rowRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtRoot = Theme.MakeTextBox(AppConfig.DefaultRoot, 0, true);
            txtRoot.TextAlignment = TextAlignment.Left;
            txtRoot.HorizontalAlignment = HorizontalAlignment.Stretch;
            txtRoot.ToolTip = "阴阳师安装根目录（含 bin\\onmyoji.exe）";
            Grid.SetColumn(txtRoot, 0);
            rowRoot.Children.Add(txtRoot);

            btnBrowse = Theme.MakeButton("浏览…", BtnBrowse_Click, ButtonKind.Secondary, 38);
            btnBrowse.Width = 72;
            btnBrowse.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(btnBrowse, 1);
            rowRoot.Children.Add(btnBrowse);
            sp.Children.Add(rowRoot);

            TextBlock hintRoot = Theme.Meta("需包含 bin\\onmyoji.exe · 修改后自动保存");
            hintRoot.Margin = new Thickness(0, 7, 0, 0);
            sp.Children.Add(hintRoot);

            return Theme.Card(sp);
        }

        private Border BuildOpenStepper()
        {
            Border box = new Border
            {
                Background = Theme.B(Theme.InputBg),
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Width = 122,
                Height = 38,
                SnapsToDevicePixels = true
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });

            Button minus = StepButton("\u2212", delegate { StepOpen(-1); }, "减少一个实例");
            Grid.SetColumn(minus, 0);
            g.Children.Add(minus);

            txtOpenNum = Theme.MakeTextBox("2", 54, false);
            txtOpenNum.Height = 34;
            txtOpenNum.Padding = new Thickness(0);
            txtOpenNum.BorderThickness = new Thickness(0);
            txtOpenNum.Background = Brushes.Transparent;
            txtOpenNum.FontFamily = Theme.FontSerif;
            txtOpenNum.FontSize = 15;
            txtOpenNum.FontWeight = FontWeights.Bold;
            txtOpenNum.TextAlignment = TextAlignment.Center;
            txtOpenNum.VerticalContentAlignment = VerticalAlignment.Center;
            txtOpenNum.ToolTip = "可直接输入 1–10 之间的整数";
            Grid.SetColumn(txtOpenNum, 1);
            g.Children.Add(txtOpenNum);

            Button plus = StepButton("+", delegate { StepOpen(1); }, "增加一个实例");
            Grid.SetColumn(plus, 2);
            g.Children.Add(plus);

            box.Child = g;
            return box;
        }

        private Button StepButton(string text, Action onClick, string tip)
        {
            Button b = new Button
            {
                Content = text,
                FontFamily = Theme.FontUI,
                FontSize = 15,
                Foreground = Theme.B(Theme.TextSub),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null,
                ToolTip = tip,
                SnapsToDevicePixels = true
            };

            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            ControlTemplate t = new ControlTemplate(typeof(Button));
            t.VisualTree = bd;
            Trigger hov = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(Button.BackgroundProperty, Theme.B(Theme.SurfaceHover)));
            hov.Setters.Add(new Setter(Button.ForegroundProperty, Theme.B(Theme.Ink)));
            t.Triggers.Add(hov);
            Trigger prs = new Trigger { Property = Button.IsPressedProperty, Value = true };
            prs.Setters.Add(new Setter(Button.BackgroundProperty, Theme.B(Color.FromRgb(0xE9, 0xDF, 0xCF))));
            t.Triggers.Add(prs);
            b.Template = t;

            b.Click += delegate { onClick(); };
            return b;
        }

        private void StepOpen(int delta)
        {
            int v;
            if (!int.TryParse(txtOpenNum.Text.Trim(), out v)) v = 2;
            v = Math.Max(1, Math.Min(10, v + delta));
            txtOpenNum.Text = v.ToString();
            txtOpenNum.CaretIndex = txtOpenNum.Text.Length;
        }

        // ==================== 02 窗口排列 ====================
        private Border BuildArrangeCard()
        {
            StackPanel sp = new StackPanel();
            sp.Children.Add(Theme.CardHeader("02", "窗口排列", "探测-定位两阶段，适配客户端尺寸锁定", null));

            Grid rowCols = new Grid();
            rowCols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowCols.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel colLabel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            colLabel.Children.Add(Theme.Text("每行窗口数", 12, FontWeights.SemiBold, Theme.Ink));
            colLabel.Children.Add(Theme.Meta("按网格自动排布"));
            Grid.SetColumn(colLabel, 0);
            rowCols.Children.Add(colLabel);

            UIElement segments = BuildColSegments();
            Grid.SetColumn(segments, 1);
            rowCols.Children.Add(segments);
            sp.Children.Add(rowCols);

            Grid rowButtons = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            rowButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(128) });
            rowButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            rowButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            btnArrange = Theme.MakeButton("排列窗口", delegate { RunTask(BtnArrange_Click()); }, ButtonKind.Primary, 40);
            Grid.SetColumn(btnArrange, 0);
            rowButtons.Children.Add(btnArrange);

            btnRefresh = Theme.MakeButton("刷新列表", BtnRefresh_Click, ButtonKind.Secondary, 40);
            Grid.SetColumn(btnRefresh, 2);
            rowButtons.Children.Add(btnRefresh);
            sp.Children.Add(rowButtons);

            TextBlock hint = Theme.Meta("先启动游戏，再刷新列表；可重复排列以微调位置。");
            hint.Margin = new Thickness(0, 9, 0, 0);
            sp.Children.Add(hint);

            return Theme.Card(sp);
        }

        private UIElement BuildColSegments()
        {
            Border wrap = new Border
            {
                Background = Theme.B(Theme.SurfaceSoft),
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right,
                SnapsToDevicePixels = true
            };

            Grid g = new Grid();
            for (int i = 0; i < 4; i++)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                int value = i + 1;
                TextBlock label = new TextBlock
                {
                    Text = value.ToString(),
                    FontFamily = Theme.FontSerif,
                    FontSize = 11,
                    Foreground = Theme.B(Theme.TextSub),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Border seg = new Border
                {
                    Height = 30,
                    CornerRadius = new CornerRadius(2),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = label,
                    Tag = false,
                    ToolTip = "每行 " + value + " 个窗口"
                };
                seg.MouseEnter += delegate
                {
                    if (!(bool)seg.Tag) seg.Background = Theme.B(Theme.SurfaceHover);
                };
                seg.MouseLeave += delegate
                {
                    if (!(bool)seg.Tag) seg.Background = Brushes.Transparent;
                };
                seg.MouseLeftButtonDown += delegate { SetArrangeCols(value); };
                _colSegments[i] = seg;
                _colLabels[i] = label;
                Grid.SetColumn(seg, i);
                g.Children.Add(seg);
            }
            wrap.Child = g;
            SetArrangeCols(2);
            return wrap;
        }

        private void SetArrangeCols(int cols)
        {
            _arrangeCols = cols;
            for (int i = 0; i < 4; i++)
            {
                bool on = (i + 1) == cols;
                _colSegments[i].Tag = on;
                _colSegments[i].Background = on ? Theme.B(Theme.Ink) : Brushes.Transparent;
                _colLabels[i].Foreground = Theme.B(on ? Colors.White : Theme.TextSub);
                _colLabels[i].FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        // ==================== 03 声音跟随 ====================
        private Border BuildSoundCard()
        {
            swSound = new SwitchControl(true);
            swSound.Changed += ChkSound_Changed;
            swSound.ToolTip = "开启 / 关闭声音跟随聚焦";

            StackPanel sp = new StackPanel();
            sp.Children.Add(Theme.CardHeader("03", "声音跟随聚焦", "前台实例正常音量，其余自动降至 15%", swSound));

            StackPanel statusRow = new StackPanel { Orientation = Orientation.Horizontal };
            _soundDot = Theme.Dot(5, Theme.Green);
            statusRow.Children.Add(_soundDot);
            lblSoundState = new TextBlock
            {
                Text = "监控中",
                FontFamily = Theme.FontUI,
                FontSize = 10.8,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.B(Theme.Green),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            statusRow.Children.Add(lblSoundState);
            statusRow.Children.Add(new TextBlock
            {
                Text = "· 每 500ms 检测前台窗口",
                FontFamily = Theme.FontUI,
                FontSize = 10.5,
                Foreground = Theme.B(Theme.TextMuted),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            });
            sp.Children.Add(statusRow);

            TextBlock meta = Theme.Meta("WASAPI 调音 · 不改游戏文件 · 失焦即降音");
            meta.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(meta);

            return Theme.Card(sp);
        }

        // ==================== 04 运行实例 ====================
        private Border BuildInstancesCard()
        {
            Grid card = new Grid();
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 标题右侧：数量 + 刷新
            Border countBadge = new Border
            {
                Background = Theme.B(Theme.SurfaceSoft),
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(7, 2, 7, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            lblWinCount = new TextBlock
            {
                Text = "0 个窗口",
                FontFamily = Theme.FontUI,
                FontSize = 10.2,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.B(Theme.TextSub)
            };
            countBadge.Child = lblWinCount;

            StackPanel headExtra = new StackPanel { Orientation = Orientation.Horizontal };
            headExtra.Children.Add(countBadge);
            Button refreshIcon = Theme.MakeIconButton("\uE72C", "刷新窗口列表", BtnRefresh_Click);
            refreshIcon.Margin = new Thickness(8, 0, 0, 0);
            headExtra.Children.Add(refreshIcon);

            card.Children.Add(Theme.CardHeader("04", "运行实例", "自动识别当前打开的阴阳师游戏窗口", headExtra));

            // 表头
            StackPanel colHead = new StackPanel();
            Grid ch = new Grid { Margin = new Thickness(2, 0, 2, 6) };
            ch.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            ch.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
            ch.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ch.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            AddHeadCell(ch, "#", 0);
            AddHeadCell(ch, "PID", 1);
            AddHeadCell(ch, "窗口标题", 2);
            TextBlock headStatus = Theme.Text("状态", 9.8, FontWeights.Normal, Theme.TextMuted);
            headStatus.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(headStatus, 3);
            ch.Children.Add(headStatus);
            colHead.Children.Add(ch);
            colHead.Children.Add(Theme.Hairline());
            Grid.SetRow(colHead, 1);
            card.Children.Add(colHead);

            // 列表 + 空态
            Grid listHost = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            _instancePanel = new StackPanel();
            _instanceScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _instancePanel,
                Padding = new Thickness(0)
            };
            listHost.Children.Add(_instanceScroll);
            _emptyState = BuildEmptyState();
            listHost.Children.Add(_emptyState);
            Grid.SetRow(listHost, 2);
            card.Children.Add(listHost);

            // 底栏：统计 + 一键关闭
            Border foot = new Border
            {
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 10, 0, 0),
                Padding = new Thickness(0, 9, 0, 0)
            };
            Grid footGrid = new Grid();
            footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            lblScan = new TextBlock
            {
                Text = "共 0 个 · 尚未刷新",
                FontFamily = Theme.FontUI,
                FontSize = 10,
                Foreground = Theme.B(Theme.TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            };
            footGrid.Children.Add(lblScan);

            btnCloseAll = Theme.MakeButton("关闭所有窗口", delegate { RunTask(BtnCloseAll_Click()); }, ButtonKind.Danger, 30);
            btnCloseAll.Width = 126;
            btnCloseAll.FontSize = 11.2;
            btnCloseAll.HorizontalAlignment = HorizontalAlignment.Right;
            btnCloseAll.ToolTip = "优雅退出所有阴阳师窗口，3 秒内未退出则强制结束";
            Grid.SetColumn(btnCloseAll, 1);
            footGrid.Children.Add(btnCloseAll);

            foot.Child = footGrid;
            Grid.SetRow(foot, 3);
            card.Children.Add(foot);

            Border outer = Theme.Card(card);
            outer.Padding = new Thickness(18, 14, 18, 14);
            return outer;
        }

        private static void AddHeadCell(Grid g, string text, int col)
        {
            TextBlock t = Theme.Text(text, 9.8, FontWeights.Normal, Theme.TextMuted);
            Grid.SetColumn(t, col);
            g.Children.Add(t);
        }

        private Border BuildInstanceRow(int index, YysWindow w)
        {
            Border row = new Border
            {
                Height = 44,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 0, 2, 0)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });

            TextBlock idx = new TextBlock
            {
                Text = index.ToString("00"),
                FontFamily = Theme.FontSerif,
                FontSize = 10.4,
                Foreground = Theme.B(Theme.TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(idx, 0);
            g.Children.Add(idx);

            TextBlock pid = new TextBlock
            {
                Text = w.Pid.ToString(),
                FontFamily = Theme.FontMono,
                FontSize = 10.4,
                Foreground = Theme.B(Theme.TextSub),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(pid, 1);
            g.Children.Add(pid);

            TextBlock title = new TextBlock
            {
                Text = w.Title,
                FontFamily = Theme.FontUI,
                FontSize = 11.8,
                Foreground = Theme.B(Theme.Ink),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = w.Title
            };
            Grid.SetColumn(title, 2);
            g.Children.Add(title);

            StackPanel state = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            state.Children.Add(Theme.Dot(5, w.Focused ? Theme.Accent : Theme.Green));
            state.Children.Add(new TextBlock
            {
                Text = w.Focused ? "聚焦" : "运行中",
                FontFamily = Theme.FontUI,
                FontSize = 10.2,
                Foreground = Theme.B(w.Focused ? Theme.Accent : Theme.TextSub),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            });
            Grid.SetColumn(state, 3);
            g.Children.Add(state);

            row.Child = g;
            row.MouseEnter += delegate { row.Background = Theme.B(Theme.SurfaceSoft); };
            row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
            return row;
        }

        private Border BuildEmptyState()
        {
            StackPanel sp = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };

            Ellipse ring = Theme.Ring(42, Theme.LineStrong, 1);
            ring.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(ring);

            TextBlock title = Theme.Text("尚未发现游戏窗口", 12.2, FontWeights.SemiBold, Theme.Ink);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.Margin = new Thickness(0, 10, 0, 0);
            sp.Children.Add(title);

            TextBlock desc = Theme.Label("启动游戏后点击「刷新列表」", 10.4, Theme.TextMuted);
            desc.HorizontalAlignment = HorizontalAlignment.Center;
            desc.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(desc);

            Button refresh = Theme.MakeButton("刷新列表", BtnRefresh_Click, ButtonKind.Secondary, 30);
            refresh.Width = 92;
            refresh.FontSize = 10.8;
            refresh.HorizontalAlignment = HorizontalAlignment.Center;
            refresh.Margin = new Thickness(0, 12, 0, 0);
            sp.Children.Add(refresh);

            return new Border { Child = sp, Background = Brushes.Transparent };
        }

        // ==================== 运行日志 ====================
        private Border BuildLogCard()
        {
            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(Theme.Dot(5, Theme.Accent));
            left.Children.Add(new TextBlock
            {
                Text = "运行日志",
                FontFamily = Theme.FontUI,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.B(Theme.TerminalText),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(7, 0, 0, 0)
            });
            Border live = new Border
            {
                Background = Theme.B(Color.FromRgb(0x2A, 0x25, 0x1F)),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(9, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "LIVE",
                    FontFamily = Theme.FontSerif,
                    FontSize = 8.2,
                    FontWeight = FontWeights.Bold,
                    Foreground = Theme.B(Theme.TerminalDim)
                }
            };
            left.Children.Add(live);
            Grid.SetColumn(left, 0);
            head.Children.Add(left);

            Button clear = Theme.MakeButton("清空", delegate { if (txtLog != null) txtLog.Clear(); }, ButtonKind.OnDark, 26);
            clear.Width = 58;
            clear.FontSize = 10.5;
            clear.HorizontalAlignment = HorizontalAlignment.Right;
            clear.ToolTip = "清空当前界面日志";
            Grid.SetColumn(clear, 1);
            head.Children.Add(clear);
            g.Children.Add(head);

            Border line = new Border { Height = 1, Background = Theme.B(Theme.TerminalLine), Margin = new Thickness(0, 10, 0, 9) };
            Grid.SetRow(line, 1);
            g.Children.Add(line);

            txtLog = Theme.MakeLogBox();
            Grid.SetRow(txtLog, 2);
            g.Children.Add(txtLog);

            Border card = new Border
            {
                Background = Theme.B(Theme.TerminalBg),
                BorderBrush = Theme.B(Color.FromRgb(0x2C, 0x26, 0x1F)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(16, 12, 14, 12),
                Child = g,
                SnapsToDevicePixels = true
            };
            return card;
        }

        // ==================== 关于弹层 ====================
        private Grid BuildAboutOverlay()
        {
            Grid overlay = new Grid { Visibility = Visibility.Collapsed, Opacity = 1 };

            Border dim = new Border { Background = Theme.BA(0xB8, Theme.Ink), CornerRadius = new CornerRadius(12) };
            dim.MouseLeftButtonDown += delegate { CloseAbout(); };
            overlay.Children.Add(dim);

            Border card = new Border
            {
                Width = 548,
                Background = Theme.B(Theme.Surface),
                BorderBrush = Theme.B(Theme.LineStrong),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(28, 22, 28, 22),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            card.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };

            StackPanel sp = new StackPanel();

            Grid head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal };
            brand.Children.Add(Theme.Seal(38));
            StackPanel bt = new StackPanel { Margin = new Thickness(13, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            bt.Children.Add(new TextBlock
            {
                Text = "阴阳师工具",
                FontFamily = Theme.FontUI,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.B(Theme.Ink)
            });
            bt.Children.Add(new TextBlock
            {
                Text = "ONMYOJI CONSOLE · v3.0 朱印界面",
                FontFamily = Theme.FontSerif,
                FontSize = 8.8,
                Foreground = Theme.B(Theme.TextMuted),
                Margin = new Thickness(0, 3, 0, 0)
            });
            brand.Children.Add(bt);
            Grid.SetColumn(brand, 0);
            head.Children.Add(brand);

            Button close = Theme.MakeChromeButton("\uE8BB", false, CloseAbout, "关闭");
            close.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(close, 1);
            head.Children.Add(close);
            sp.Children.Add(head);

            TextBlock intro = new TextBlock
            {
                Text = "为多开而生的桌面控制台。启动、排列、声音跟随，一屏完成。",
                FontFamily = Theme.FontUI,
                FontSize = 11.8,
                Foreground = Theme.B(Theme.TextSub),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 14, 0, 0)
            };
            sp.Children.Add(intro);

            sp.Children.Add(Theme.Divider(16, 10));

            sp.Children.Add(AboutRow("01", "多开控制", "按目标开数补足实例，间隔 8 秒启动，避免客户端抢占失败。"));
            sp.Children.Add(AboutRow("02", "窗口排列", "先探测客户端接受的尺寸，再按 1–4 列网格自动排布。"));
            sp.Children.Add(AboutRow("03", "声音跟随", "聚焦实例恢复正常音量，其余自动降至 15%，全部失焦则全部降音。"));

            sp.Children.Add(Theme.Divider(14, 12));

            Border note = new Border
            {
                Background = Theme.B(Theme.SurfaceSoft),
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(12, 10, 12, 10)
            };
            StackPanel noteSp = new StackPanel();
            noteSp.Children.Add(new TextBlock
            {
                Text = "权限与安全",
                FontFamily = Theme.FontUI,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.B(Theme.Ink)
            });
            TextBlock noteBody = new TextBlock
            {
                Text = "操作其他进程窗口与音量需要管理员权限，建议右键以管理员身份运行。本工具不修改任何游戏文件，音量调节通过 Windows WASAPI 完成。",
                FontFamily = Theme.FontUI,
                FontSize = 10.4,
                Foreground = Theme.B(Theme.TextSub),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 17,
                Margin = new Thickness(0, 4, 0, 0)
            };
            noteSp.Children.Add(noteBody);
            note.Child = noteSp;
            sp.Children.Add(note);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
            Button doc = Theme.MakeButton("打开使用说明", delegate { CloseAbout(); OpenUsageDoc(); }, ButtonKind.Secondary, 34);
            doc.Width = 120;
            doc.FontSize = 11;
            doc.Margin = new Thickness(0, 0, 8, 0);
            buttons.Children.Add(doc);
            Button ok = Theme.MakeButton("知道了", CloseAbout, ButtonKind.Primary, 34);
            ok.Width = 92;
            ok.FontSize = 11;
            buttons.Children.Add(ok);
            sp.Children.Add(buttons);

            card.Child = sp;
            overlay.Children.Add(card);
            return overlay;
        }

        private static UIElement AboutRow(string index, string title, string desc)
        {
            Grid g = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock idx = new TextBlock
            {
                Text = index,
                FontFamily = Theme.FontSerif,
                FontSize = 10.2,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.B(Theme.Gold),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };
            Grid.SetColumn(idx, 0);
            g.Children.Add(idx);

            StackPanel right = new StackPanel();
            right.Children.Add(new TextBlock
            {
                Text = title,
                FontFamily = Theme.FontUI,
                FontSize = 11.8,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.B(Theme.Ink)
            });
            right.Children.Add(new TextBlock
            {
                Text = desc,
                FontFamily = Theme.FontUI,
                FontSize = 10.3,
                Foreground = Theme.B(Theme.TextSub),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(right, 1);
            g.Children.Add(right);
            return g;
        }

        // 观察后台任务异常，避免未处理异常静默丢失
        private void RunTask(System.Threading.Tasks.Task t)
        {
            if (t == null) return;
            t.ContinueWith(delegate(System.Threading.Tasks.Task x)
            {
                Exception ex = x.Exception == null ? null : x.Exception.GetBaseException();
                Log("操作失败：" + (ex == null ? "未知错误" : ex.Message));
            }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}

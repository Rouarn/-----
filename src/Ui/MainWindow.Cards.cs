using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using OnmyojiTools.App;
using OnmyojiTools.Core;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Ui
{
    /// <summary>主窗口的卡片、实例列表、日志与弹层（视图构建部分）。</summary>
    internal sealed partial class MainWindow
    {
        // ==================== 01 多开控制 ====================
        private Border BuildLaunchCard(int target)
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

            _openStepper = BuildOpenStepper(target);
            Grid.SetColumn(_openStepper, 1);
            rowOpen.Children.Add(_openStepper);
            sp.Children.Add(rowOpen);

            Grid actions = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            btnOpen = Theme.MakeButton("开始多开", delegate { BtnOpen_Click(); }, ButtonKind.Primary, 42);
            Grid.SetColumn(btnOpen, 0);
            actions.Children.Add(btnOpen);

            btnSession = Theme.MakeButton("一键会话", delegate { BtnSession_Click(); }, ButtonKind.Secondary, 42);
            btnSession.ToolTip = "补足实例，等待窗口可响应后按当前列数/显示器排列";
            Grid.SetColumn(btnSession, 2);
            actions.Children.Add(btnSession);
            sp.Children.Add(actions);

            TextBlock sessionHint = Theme.Meta("一键会话 = 补足实例 → 等待就绪 → 排列");
            sessionHint.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(sessionHint);

            sp.Children.Add(Theme.Divider(14, 10));
            sp.Children.Add(Theme.Meta("游戏工作目录"));
            Grid rowRoot = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            rowRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtRoot = Theme.MakeTextBox(AppSettings.DefaultGameRoot, 0, true);
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

        private Border BuildOpenStepper(int initial)
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

            txtOpenNum = Theme.MakeTextBox(initial.ToString(), 54, false);
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
            txtOpenNum.LostFocus += delegate
            {
                int value;
                if (InputValidation.TryParseInt(txtOpenNum.Text, AppSettings.MinInstances, AppSettings.MaxInstances, out value))
                {
                    _controller.UpdateTargetInstances(value);
                }
                else
                {
                    txtOpenNum.Text = _controller.Settings.TargetInstances.ToString();
                }
            };
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
            Trigger hover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Button.BackgroundProperty, Theme.B(Theme.SurfaceHover)));
            hover.Setters.Add(new Setter(Button.ForegroundProperty, Theme.B(Theme.Ink)));
            t.Triggers.Add(hover);
            Trigger pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Button.BackgroundProperty, Theme.B(Color.FromRgb(0xE9, 0xDF, 0xCF))));
            t.Triggers.Add(pressed);
            b.Template = t;

            b.Click += delegate { onClick(); };
            return b;
        }

        // ==================== 02 窗口排列 ====================
        private Border BuildArrangeCard(int columns)
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

            _colWrap = BuildColSegments(columns);
            Grid.SetColumn(_colWrap, 1);
            rowCols.Children.Add(_colWrap);
            sp.Children.Add(rowCols);

            Grid rowButtons = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            rowButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            btnArrange = Theme.MakeButton("排列窗口", delegate { BtnArrange_Click(); }, ButtonKind.Primary, 40);
            Grid.SetColumn(btnArrange, 0);
            rowButtons.Children.Add(btnArrange);
            sp.Children.Add(rowButtons);

            TextBlock hint = Theme.Meta("在「运行实例」右上角刷新列表；可重复排列以微调位置。");
            hint.Margin = new Thickness(0, 9, 0, 0);
            sp.Children.Add(hint);

            return Theme.Card(sp);
        }

        private Border BuildColSegments(int selected)
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
                Border segment = new Border
                {
                    Height = 30,
                    CornerRadius = new CornerRadius(2),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = label,
                    Tag = false,
                    ToolTip = "每行 " + value + " 个窗口"
                };
                segment.MouseEnter += delegate
                {
                    if (!(bool)segment.Tag) segment.Background = Theme.B(Theme.SurfaceHover);
                };
                segment.MouseLeave += delegate
                {
                    if (!(bool)segment.Tag) segment.Background = Brushes.Transparent;
                };
                segment.MouseLeftButtonDown += delegate { SetArrangeCols(value, true); };
                _colSegments[i] = segment;
                _colLabels[i] = label;
                Grid.SetColumn(segment, i);
                g.Children.Add(segment);
            }

            wrap.Child = g;
            SetArrangeCols(selected, false);
            return wrap;
        }

        // ==================== 03 声音跟随 ====================
        private Border BuildSoundCard(bool initial)
        {
            swSound = new SwitchControl(initial);
            swSound.Changed += OnSoundChanged;
            swSound.ToolTip = "开启 / 关闭声音跟随聚焦";

            StackPanel sp = new StackPanel();
            sp.Children.Add(Theme.CardHeader("03", "声音跟随聚焦", "前台实例正常音量，其余自动降至设定值", swSound));

            StackPanel statusRow = new StackPanel { Orientation = Orientation.Horizontal };
            _soundDot = Theme.Dot(5, initial ? Theme.Green : Theme.TextMuted);
            statusRow.Children.Add(_soundDot);
            lblSoundState = new TextBlock
            {
                Text = initial ? "监控中" : "已暂停",
                FontFamily = Theme.FontUI,
                FontSize = 10.8,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.B(initial ? Theme.Green : Theme.TextMuted),
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
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            card.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

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
            btnRefresh = Theme.MakeIconButton("\uE72C", "刷新实例列表（F5）", delegate { BtnRefresh_Click(); });
            btnRefresh.Margin = new Thickness(8, 0, 0, 0);
            headExtra.Children.Add(btnRefresh);

            card.Children.Add(Theme.CardHeader("04", "运行实例", "自动识别当前打开的阴阳师游戏窗口 · 右键可操作", headExtra));

            _hiddenBanner = new Border
            {
                Background = Theme.B(Theme.AccentSoft),
                BorderBrush = Theme.B(Theme.AccentLine),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(9, 4, 5, 4),
                Margin = new Thickness(0, 0, 0, 7),
                Visibility = Visibility.Collapsed
            };
            Grid hiddenGrid = new Grid();
            hiddenGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hiddenGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _hiddenText = new TextBlock
            {
                Text = "已隐藏 0 个游戏窗口",
                FontFamily = Theme.FontUI,
                FontSize = 10.3,
                Foreground = Theme.B(Theme.Accent),
                VerticalAlignment = VerticalAlignment.Center
            };
            hiddenGrid.Children.Add(_hiddenText);
            Button showHidden = Theme.MakeButton("恢复", delegate { ShowHiddenWindows(); }, ButtonKind.Link, 22);
            showHidden.FontSize = 10.5;
            showHidden.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(showHidden, 1);
            hiddenGrid.Children.Add(showHidden);
            _hiddenBanner.Child = hiddenGrid;
            Grid.SetRow(_hiddenBanner, 1);
            card.Children.Add(_hiddenBanner);

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
            Grid.SetRow(colHead, 2);
            card.Children.Add(colHead);

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
            Grid.SetRow(listHost, 3);
            card.Children.Add(listHost);

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
            footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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

            _btnResources = Theme.MakeButton("资源", delegate { OpenResources(); }, ButtonKind.Link, 30);
            _btnResources.Width = 58;
            _btnResources.FontSize = 10.8;
            _btnResources.HorizontalAlignment = HorizontalAlignment.Right;
            _btnResources.Margin = new Thickness(0, 0, 8, 0);
            _btnResources.ToolTip = "查看实例内存、CPU 累计时间与运行时长";
            Grid.SetColumn(_btnResources, 2);
            footGrid.Children.Add(_btnResources);

            _btnPreview = Theme.MakeButton("预览", delegate { OpenPreview(); }, ButtonKind.Link, 30);
            _btnPreview.Width = 58;
            _btnPreview.FontSize = 10.8;
            _btnPreview.HorizontalAlignment = HorizontalAlignment.Right;
            _btnPreview.Margin = new Thickness(0, 0, 8, 0);
            _btnPreview.ToolTip = "DWM 实时预览当前游戏窗口，点击缩略图可聚焦";
            Grid.SetColumn(_btnPreview, 1);
            footGrid.Children.Add(_btnPreview);

            _btnHideAll = Theme.MakeButton("隐藏全部", delegate { BtnHideAll_Click(); }, ButtonKind.Secondary, 30);
            _btnHideAll.Width = 106;
            _btnHideAll.FontSize = 11;
            _btnHideAll.Margin = new Thickness(0, 0, 8, 0);
            _btnHideAll.HorizontalAlignment = HorizontalAlignment.Right;
            _btnHideAll.ToolTip = "隐藏全部游戏窗口（Ctrl+Alt+H）";
            Grid.SetColumn(_btnHideAll, 3);
            footGrid.Children.Add(_btnHideAll);

            btnCloseAll = Theme.MakeButton("关闭所有窗口", delegate { BtnCloseAll_Click(); }, ButtonKind.Danger, 30);
            btnCloseAll.Width = 126;
            btnCloseAll.FontSize = 11.2;
            btnCloseAll.HorizontalAlignment = HorizontalAlignment.Right;
            btnCloseAll.ToolTip = "优雅退出所有阴阳师窗口，3 秒内未退出则强制结束";
            Grid.SetColumn(btnCloseAll, 4);
            footGrid.Children.Add(btnCloseAll);

            foot.Child = footGrid;
            Grid.SetRow(foot, 4);
            card.Children.Add(foot);

            Border outer = Theme.Card(card);
            outer.Padding = new Thickness(18, 14, 18, 14);
            return outer;
        }

        private static void AddHeadCell(Grid g, string text, int column)
        {
            TextBlock label = Theme.Text(text, 9.8, FontWeights.Normal, Theme.TextMuted);
            Grid.SetColumn(label, column);
            g.Children.Add(label);
        }

        private Border BuildInstanceRow(int index, GameWindowInfo window, string customLabel)
        {
            Border row = new Border
            {
                Height = _compactMode ? 38 : 44,
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 0, 2, 0)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });

            TextBlock indexLabel = new TextBlock
            {
                Text = index.ToString("00"),
                FontFamily = Theme.FontSerif,
                FontSize = _compactMode ? 9.8 : 10.4,
                Foreground = Theme.B(Theme.TextMuted),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(indexLabel, 0);
            g.Children.Add(indexLabel);

            TextBlock pid = new TextBlock
            {
                Text = window.ProcessId.ToString(),
                FontFamily = Theme.FontMono,
                FontSize = _compactMode ? 9.8 : 10.4,
                Foreground = Theme.B(Theme.TextSub),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(pid, 1);
            g.Children.Add(pid);

            Grid titleCell = new Grid { VerticalAlignment = VerticalAlignment.Center };
            titleCell.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleCell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            if (!string.IsNullOrEmpty(customLabel))
            {
                Border badge = new Border
                {
                    Background = Theme.B(Theme.AccentSoft),
                    BorderBrush = Theme.B(Theme.AccentLine),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                    Padding = new Thickness(5, 1, 5, 1),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    MaxWidth = 100
                };
                TextBlock labelText = Theme.Text(customLabel, 9.2, FontWeights.SemiBold, Theme.Accent);
                labelText.TextTrimming = TextTrimming.CharacterEllipsis;
                labelText.ToolTip = customLabel;
                badge.Child = labelText;
                titleCell.Children.Add(badge);
            }

            TextBlock title = new TextBlock
            {
                Text = window.Title,
                FontFamily = Theme.FontUI,
                FontSize = _compactMode ? 11.2 : 11.8,
                Foreground = Theme.B(Theme.Ink),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = window.Title
            };
            Grid.SetColumn(title, 1);
            titleCell.Children.Add(title);

            Grid.SetColumn(titleCell, 2);
            g.Children.Add(titleCell);

            StackPanel state = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Ellipse statusDot = Theme.Dot(5, window.IsForeground ? Theme.Accent : Theme.Green);
            TextBlock statusLabel = new TextBlock
            {
                Text = window.IsForeground ? "聚焦" : "运行中",
                FontFamily = Theme.FontUI,
                FontSize = _compactMode ? 9.8 : 10.2,
                Foreground = Theme.B(window.IsForeground ? Theme.Accent : Theme.TextSub),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            };
            state.Children.Add(statusDot);
            state.Children.Add(statusLabel);
            Grid.SetColumn(state, 3);
            g.Children.Add(state);

            _rows.Add(new InstanceRow
            {
                ProcessId = window.ProcessId,
                Handle = window.Handle,
                Title = window.Title,
                CustomLabel = customLabel,
                Dot = statusDot,
                Label = statusLabel,
                IsFocused = window.IsForeground
            });

            InstanceRow info = _rows[_rows.Count - 1];
            row.ContextMenu = BuildRowMenu(info);
            row.ToolTip = "右键：聚焦、标签、音量、隐藏、关闭或重启；拖动可交换位置";
            row.Tag = info;
            row.AllowDrop = true;
            row.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                _dragSource = info;
                _dragStart = e.GetPosition(this);
            };
            row.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (e.LeftButton != MouseButtonState.Pressed || _dragSource != info) return;
                Point current = e.GetPosition(this);
                if (Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                DragDrop.DoDragDrop(row, info, DragDropEffects.Move);
            };
            row.DragOver += delegate(object sender, DragEventArgs e)
            {
                e.Effects = e.Data.GetDataPresent(typeof(InstanceRow)) ? DragDropEffects.Move : DragDropEffects.None;
                e.Handled = true;
            };
            row.Drop += delegate(object sender, DragEventArgs e)
            {
                InstanceRow target = e.Data.GetData(typeof(InstanceRow)) as InstanceRow;
                if (target == null || target == info) return;
                if (_controller.SwapWindowPositions(target.Handle, info.Handle))
                {
                    _logger.Log("已通过拖动交换两个实例的位置。");
                    RunRefresh();
                }
                e.Handled = true;
            };
            row.Child = g;
            row.MouseEnter += delegate { row.Background = Theme.B(Theme.SurfaceSoft); };
            row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
            return row;
        }

        private ContextMenu BuildRowMenu(InstanceRow info)
        {
            ContextMenu menu = new ContextMenu();
            Theme.ApplyContextMenuStyle(menu);

            menu.Items.Add(RowMenuItem("聚焦", delegate { FocusRow(info); }));
            menu.Items.Add(RowMenuItem("设置标签…", delegate { EditLabelFromMenu(info); }));
            menu.Items.Add(RowMenuItem("最小化", delegate { _controller.MinimizeWindow(info.Handle); }));
            menu.Items.Add(RowMenuItem("隐藏", delegate
            {
                _controller.HideWindow(info.Handle);
                _logger.Log("实例 PID " + info.ProcessId + " 已隐藏。");
            }));
            menu.Items.Add(new Separator());

            MenuItem volume = new MenuItem { Header = "音量" };
            volume.Items.Add(RowMenuItem("100%", delegate { _controller.SetInstanceVolume(info.ProcessId, 1.0f); }));
            volume.Items.Add(RowMenuItem("75%", delegate { _controller.SetInstanceVolume(info.ProcessId, 0.75f); }));
            volume.Items.Add(RowMenuItem("50%", delegate { _controller.SetInstanceVolume(info.ProcessId, 0.5f); }));
            volume.Items.Add(RowMenuItem("25%", delegate { _controller.SetInstanceVolume(info.ProcessId, 0.25f); }));
            volume.Items.Add(RowMenuItem("静音", delegate { _controller.SetInstanceVolume(info.ProcessId, 0f); }));
            menu.Items.Add(volume);
            menu.Items.Add(RowMenuItem("恢复声音跟随", delegate { _controller.ClearInstanceVolume(info.ProcessId); }));
            menu.Items.Add(new Separator());

            menu.Items.Add(RowMenuItem("关闭实例", delegate { CloseInstanceFromMenu(info); }));
            menu.Items.Add(RowMenuItem("重启实例", delegate { RestartInstanceFromMenu(info); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(RowMenuItem("复制 PID", delegate { CopyToClipboard(info.ProcessId.ToString()); }));
            menu.Items.Add(RowMenuItem("复制窗口标题", delegate { CopyToClipboard(info.Title); }));
            return menu;
        }

        private static MenuItem RowMenuItem(string text, Action action)
        {
            MenuItem item = new MenuItem { Header = text };
            item.Click += delegate { action(); };
            return item;
        }

        private void CopyToClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text ?? "");
                _logger.Log("已复制到剪贴板：" + text);
            }
            catch (Exception ex)
            {
                _logger.Log("复制失败：" + ex.Message);
            }
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

            _emptyTitle = Theme.Text("尚未发现游戏窗口", 12.2, FontWeights.SemiBold, Theme.Ink);
            _emptyTitle.HorizontalAlignment = HorizontalAlignment.Center;
            _emptyTitle.Margin = new Thickness(0, 10, 0, 0);
            sp.Children.Add(_emptyTitle);

            _emptyDesc = Theme.Label("启动游戏后点「运行实例」右上角刷新，或按 F5", 10.4, Theme.TextMuted);
            _emptyDesc.HorizontalAlignment = HorizontalAlignment.Center;
            _emptyDesc.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(_emptyDesc);

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

            Border divider = new Border { Height = 1, Background = Theme.B(Theme.TerminalLine), Margin = new Thickness(0, 10, 0, 9) };
            Grid.SetRow(divider, 1);
            g.Children.Add(divider);

            txtLog = Theme.MakeLogBox();
            Grid.SetRow(txtLog, 2);
            g.Children.Add(txtLog);

            return new Border
            {
                Background = Theme.B(Theme.TerminalBg),
                BorderBrush = Theme.B(Color.FromRgb(0x2C, 0x26, 0x1F)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(16, 12, 14, 12),
                Child = g,
                SnapsToDevicePixels = true
            };
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
            card.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };

            StackPanel sp = new StackPanel();

            Grid head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal };
            brand.Children.Add(Theme.Seal(38));
            StackPanel brandText = new StackPanel { Margin = new Thickness(13, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            brandText.Children.Add(new TextBlock
            {
                Text = "阴阳师工具",
                FontFamily = Theme.FontUI,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.B(Theme.Ink)
            });
            brandText.Children.Add(new TextBlock
            {
                Text = "ONMYOJI CONSOLE · v3.0 朱印界面",
                FontFamily = Theme.FontSerif,
                FontSize = 8.8,
                Foreground = Theme.B(Theme.TextMuted),
                Margin = new Thickness(0, 3, 0, 0)
            });
            brand.Children.Add(brandText);
            Grid.SetColumn(brand, 0);
            head.Children.Add(brand);

            Button close = Theme.MakeChromeButton("\uE8BB", false, CloseAbout, "关闭");
            close.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(close, 1);
            head.Children.Add(close);
            sp.Children.Add(head);

            sp.Children.Add(new TextBlock
            {
                Text = "为多开而生的桌面控制台。启动、排列、声音跟随，一屏完成。",
                FontFamily = Theme.FontUI,
                FontSize = 11.8,
                Foreground = Theme.B(Theme.TextSub),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 14, 0, 0)
            });

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
            StackPanel noteContent = new StackPanel();
            noteContent.Children.Add(new TextBlock
            {
                Text = "权限与安全",
                FontFamily = Theme.FontUI,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.B(Theme.Ink)
            });
            noteContent.Children.Add(new TextBlock
            {
                Text = "操作其他进程窗口与音量需要管理员权限，建议右键以管理员身份运行。本工具不修改任何游戏文件，音量调节通过 Windows WASAPI 完成。",
                FontFamily = Theme.FontUI,
                FontSize = 10.4,
                Foreground = Theme.B(Theme.TextSub),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 17,
                Margin = new Thickness(0, 4, 0, 0)
            });
            note.Child = noteContent;
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

        // ==================== 确认弹层 ====================
        private Grid BuildConfirmOverlay()
        {
            Grid overlay = new Grid { Visibility = Visibility.Collapsed, Opacity = 1 };

            Border dim = new Border { Background = Theme.BA(0xB8, Theme.Ink), CornerRadius = new CornerRadius(12) };
            dim.MouseLeftButtonDown += delegate { FinishConfirm(false); };
            overlay.Children.Add(dim);

            Border card = new Border
            {
                Width = 460,
                Background = Theme.B(Theme.Surface),
                BorderBrush = Theme.B(Theme.LineStrong),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(26, 22, 26, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            card.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };

            StackPanel sp = new StackPanel();
            _confirmTitle = new TextBlock
            {
                Text = "请确认",
                FontFamily = Theme.FontUI,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.B(Theme.Ink)
            };
            sp.Children.Add(_confirmTitle);

            _confirmMessage = new TextBlock
            {
                Text = "",
                FontFamily = Theme.FontUI,
                FontSize = 11.5,
                Foreground = Theme.B(Theme.TextSub),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 19,
                Margin = new Thickness(0, 10, 0, 0)
            };
            sp.Children.Add(_confirmMessage);

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };
            Button cancel = Theme.MakeButton("取消", delegate { FinishConfirm(false); }, ButtonKind.Secondary, 34);
            cancel.Width = 82;
            cancel.FontSize = 11;
            cancel.Margin = new Thickness(0, 0, 8, 0);
            buttons.Children.Add(cancel);

            _confirmOkButton = Theme.MakeButton("确认", delegate { FinishConfirm(true); }, ButtonKind.Danger, 34);
            _confirmOkButton.Width = 104;
            _confirmOkButton.FontSize = 11;
            buttons.Children.Add(_confirmOkButton);
            sp.Children.Add(buttons);

            card.Child = sp;
            overlay.Children.Add(card);
            return overlay;
        }

        // ==================== 设置弹层 ====================
        private Grid BuildSettingsOverlay()
        {
            Grid overlay = new Grid { Visibility = Visibility.Collapsed, Opacity = 1 };

            Border dim = new Border { Background = Theme.BA(0xB8, Theme.Ink), CornerRadius = new CornerRadius(12) };
            dim.MouseLeftButtonDown += delegate { CloseSettings(); };
            overlay.Children.Add(dim);

            Border card = new Border
            {
                Width = 620,
                MaxHeight = 610,
                Background = Theme.B(Theme.Surface),
                BorderBrush = Theme.B(Theme.LineStrong),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(26, 20, 26, 18),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            card.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = "设置与快捷操作",
                FontFamily = Theme.FontUI,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.B(Theme.Ink)
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "全局快捷键、布局预设、声音与启动项",
                FontFamily = Theme.FontUI,
                FontSize = 10.8,
                Foreground = Theme.B(Theme.TextSub),
                Margin = new Thickness(0, 4, 0, 0)
            });
            head.Children.Add(titleStack);
            Button close = Theme.MakeChromeButton("\uE8BB", false, CloseSettings, "关闭设置");
            close.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(close, 1);
            head.Children.Add(close);
            Grid.SetRow(head, 0);
            layout.Children.Add(head);

            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 12, 0, 4)
            };
            StackPanel sp = new StackPanel();
            sp.Children.Add(SettingsSectionTitle("全局快捷键"));

            Grid schemeRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            schemeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(148) });
            schemeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock schemeLabel = Theme.Label("快捷键方案", 10.3, Theme.TextSub);
            Grid.SetColumn(schemeLabel, 0);
            schemeRow.Children.Add(schemeLabel);
            _hotkeySchemeCombo = Theme.MakeComboBox(210);
            _hotkeySchemeCombo.HorizontalAlignment = HorizontalAlignment.Left;
            _hotkeySchemeCombo.ItemsSource = new string[] { "Ctrl+Alt（默认）", "Ctrl+Shift", "Alt+Shift" };
            _suppressHotkeyEvents = true;
            _hotkeySchemeCombo.SelectedIndex = _controller.Settings.HotkeyScheme;
            _suppressHotkeyEvents = false;
            _hotkeySchemeCombo.SelectionChanged += HotkeyScheme_SelectionChanged;
            Grid.SetColumn(_hotkeySchemeCombo, 1);
            schemeRow.Children.Add(_hotkeySchemeCombo);
            sp.Children.Add(schemeRow);

            sp.Children.Add(SettingsShortcut("Ctrl+Alt+H", "隐藏 / 恢复全部游戏窗口（老板键）", out _shortcutBossLabel));
            sp.Children.Add(SettingsShortcut("Ctrl+Alt+1…9", "聚焦第 N 个游戏窗口", out _shortcutFocusLabel));
            sp.Children.Add(SettingsShortcut("Ctrl+Alt+← / →", "在所有游戏窗口之间循环切换", out _shortcutCycleLabel));
            TextBlock localKeys;
            sp.Children.Add(SettingsShortcut("Ctrl+1…9 / Ctrl+Tab", "工具窗口聚焦时也可切换；F5 刷新列表", out localKeys));

            sp.Children.Add(Theme.Divider(15, 11));
            sp.Children.Add(SettingsSectionTitle("排列与声音"));

            Grid monitorRow = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            monitorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(148) });
            monitorRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock monitorLabel = Theme.Label("目标显示器", 10.3, Theme.TextSub);
            Grid.SetColumn(monitorLabel, 0);
            monitorRow.Children.Add(monitorLabel);

            _monitorCombo = Theme.MakeComboBox(292);
            _monitorCombo.HorizontalAlignment = HorizontalAlignment.Left;
            _monitorCombo.DisplayMemberPath = "DisplayName";
            _monitorCombo.ItemsSource = _controller.GetMonitors();
            IReadOnlyList<MonitorInfo> monitors = _controller.GetMonitors();
            int selectedMonitor = _controller.Settings.MonitorIndex;
            if (selectedMonitor < 0 || selectedMonitor >= monitors.Count) selectedMonitor = 0;
            _suppressMonitorEvents = true;
            _monitorCombo.SelectedIndex = selectedMonitor;
            _suppressMonitorEvents = false;
            _monitorCombo.SelectionChanged += MonitorCombo_SelectionChanged;
            Grid.SetColumn(_monitorCombo, 1);
            monitorRow.Children.Add(_monitorCombo);
            sp.Children.Add(monitorRow);

            Grid volumeRow = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            volumeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(148) });
            volumeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock volumeLabel = Theme.Label("失焦音量", 10.3, Theme.TextSub);
            Grid.SetColumn(volumeLabel, 0);
            volumeRow.Children.Add(volumeLabel);

            StackPanel volumeStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _backgroundSlider = Theme.MakeSlider(0, 100, _controller.Settings.BackgroundVolumePercent, 238);
            _backgroundSlider.IsSnapToTickEnabled = true;
            _backgroundSlider.TickFrequency = 5;
            volumeStack.Children.Add(_backgroundSlider);
            _backgroundValue = Theme.Text(_controller.Settings.BackgroundVolumePercent + "%", 10.3, FontWeights.SemiBold, Theme.Ink);
            _backgroundValue.Margin = new Thickness(10, 0, 0, 0);
            volumeStack.Children.Add(_backgroundValue);
            _backgroundSlider.ValueChanged += BackgroundSlider_ValueChanged;
            Grid.SetColumn(volumeStack, 1);
            volumeRow.Children.Add(volumeStack);
            sp.Children.Add(volumeRow);

            TextBlock volumeHint = Theme.Meta("失焦音量可随时调整；单实例手动音量仍优先。");
            volumeHint.Margin = new Thickness(0, 0, 0, 2);
            sp.Children.Add(volumeHint);

            Grid placementRow = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            placementRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(148) });
            placementRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock placementLabel = Theme.Label("窗口位置记忆", 10.3, Theme.TextSub);
            Grid.SetColumn(placementLabel, 0);
            placementRow.Children.Add(placementLabel);
            StackPanel placementButtons = new StackPanel { Orientation = Orientation.Horizontal };
            Button remember = Theme.MakeButton("记住当前位置", delegate { _controller.SaveWindowPlacements(); }, ButtonKind.Secondary, 28);
            remember.Width = 112;
            remember.FontSize = 10.3;
            remember.Margin = new Thickness(0, 0, 7, 0);
            placementButtons.Children.Add(remember);
            Button restore = Theme.MakeButton("恢复位置", delegate { _controller.RestoreWindowPlacements(); }, ButtonKind.Secondary, 28);
            restore.Width = 92;
            restore.FontSize = 10.3;
            placementButtons.Children.Add(restore);
            Grid.SetColumn(placementButtons, 1);
            placementRow.Children.Add(placementButtons);
            sp.Children.Add(placementRow);

            Grid autoRow = new Grid { Margin = new Thickness(0, 7, 0, 0) };
            autoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            autoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel autoText = new StackPanel();
            autoText.Children.Add(Theme.Text("新窗口自动排列", 10.8, FontWeights.SemiBold, Theme.Ink));
            autoText.Children.Add(Theme.Meta("检测到新增实例后按当前列数/显示器排列；默认关闭"));
            autoRow.Children.Add(autoText);
            _autoArrangeSwitch = new SwitchControl(_controller.Settings.AutoArrangeNewWindows);
            _autoArrangeSwitch.Changed += OnAutoArrangeChanged;
            Grid.SetColumn(_autoArrangeSwitch, 1);
            autoRow.Children.Add(_autoArrangeSwitch);
            sp.Children.Add(autoRow);

            Grid restartRow = new Grid { Margin = new Thickness(0, 7, 0, 0) };
            restartRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            restartRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel restartText = new StackPanel();
            restartText.Children.Add(Theme.Text("无响应自动重启", 10.8, FontWeights.SemiBold, Theme.Ink));
            restartText.Children.Add(Theme.Meta("连续两次无响应后关闭并补启动；30 秒内最多一次，默认关闭"));
            restartRow.Children.Add(restartText);
            _autoRestartSwitch = new SwitchControl(_controller.Settings.AutoRestartUnresponsive);
            _autoRestartSwitch.Changed += OnAutoRestartChanged;
            Grid.SetColumn(_autoRestartSwitch, 1);
            restartRow.Children.Add(_autoRestartSwitch);
            sp.Children.Add(restartRow);

            Grid dimRow = new Grid { Margin = new Thickness(0, 7, 0, 0) };
            dimRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dimRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel dimText = new StackPanel();
            dimText.Children.Add(Theme.Text("非焦点窗口半透明", 10.8, FontWeights.SemiBold, Theme.Ink));
            dimText.Children.Add(Theme.Meta("失焦实例 80% 不透明度；部分客户端可能不支持，默认关闭"));
            dimRow.Children.Add(dimText);
            _dimSwitch = new SwitchControl(_controller.Settings.DimUnfocusedWindows);
            _dimSwitch.Changed += OnDimChanged;
            Grid.SetColumn(_dimSwitch, 1);
            dimRow.Children.Add(_dimSwitch);
            sp.Children.Add(dimRow);

            sp.Children.Add(Theme.Divider(15, 11));
            sp.Children.Add(SettingsSectionTitle("布局预设"));
            sp.Children.Add(new TextBlock
            {
                Text = "保存当前列数与目标显示器，之后一键应用并重新排列；窗口槽位会在后续版本加入。",
                FontFamily = Theme.FontUI,
                FontSize = 10.4,
                Foreground = Theme.B(Theme.TextSub),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            Border presetBox = new Border
            {
                Background = Theme.B(Theme.SurfaceSoft),
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(10, 6, 10, 6)
            };
            ScrollViewer presetScroll = new ScrollViewer
            {
                MaxHeight = 142,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _presetListPanel = new StackPanel();
            presetScroll.Content = _presetListPanel;
            presetBox.Child = presetScroll;
            sp.Children.Add(presetBox);

            Grid saveRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            saveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            saveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel nameStack = new StackPanel();
            TextBlock nameLabel = Theme.Meta("预设名称");
            nameLabel.Margin = new Thickness(0, 0, 0, 3);
            nameStack.Children.Add(nameLabel);
            _presetNameBox = Theme.MakeTextBox("", 200, false);
            _presetNameBox.Height = 34;
            _presetNameBox.ToolTip = "给当前布局起一个名字，例如「日常 4 开」";
            nameStack.Children.Add(_presetNameBox);
            saveRow.Children.Add(nameStack);
            Button save = Theme.MakeButton("保存当前列数", delegate { SavePreset(); }, ButtonKind.Secondary, 34);
            save.Width = 132;
            save.FontSize = 11;
            save.Margin = new Thickness(8, 0, 0, 0);
            save.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(save, 1);
            saveRow.Children.Add(save);
            sp.Children.Add(saveRow);

            _presetHint = new TextBlock
            {
                Text = "",
                FontFamily = Theme.FontUI,
                FontSize = 10.2,
                Foreground = Theme.B(Theme.TextMuted),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
            sp.Children.Add(_presetHint);

            sp.Children.Add(Theme.Divider(15, 11));
            sp.Children.Add(SettingsSectionTitle("单实例操作"));
            sp.Children.Add(new TextBlock
            {
                Text = "在 04「运行实例」列表右键任意窗口：聚焦、最小化、隐藏、音量、静音、关闭或重启。底部「隐藏全部」与老板键等效。",
                FontFamily = Theme.FontUI,
                FontSize = 10.4,
                Foreground = Theme.B(Theme.TextSub),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 17
            });

            sp.Children.Add(Theme.Divider(15, 11));
            sp.Children.Add(SettingsSectionTitle("启动与诊断"));

            Grid compactRow = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            compactRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            compactRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel compactText = new StackPanel();
            compactText.Children.Add(Theme.Text("紧凑模式", 10.8, FontWeights.SemiBold, Theme.Ink));
            compactText.Children.Add(Theme.Meta("缩窄左侧卡片与实例行高度，适合小屏或窗口并排"));
            compactRow.Children.Add(compactText);
            _compactSwitch = new SwitchControl(_controller.Settings.CompactMode);
            _compactSwitch.Changed += OnCompactChanged;
            Grid.SetColumn(_compactSwitch, 1);
            compactRow.Children.Add(_compactSwitch);
            sp.Children.Add(compactRow);

            Grid darkRow = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            darkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            darkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel darkText = new StackPanel();
            darkText.Children.Add(Theme.Text("深色主题", 10.8, FontWeights.SemiBold, Theme.Ink));
            darkText.Children.Add(Theme.Meta("墨色纸面；切换时重建界面，日志与设置保留"));
            darkRow.Children.Add(darkText);
            _darkSwitch = new SwitchControl(_controller.Settings.DarkTheme);
            _darkSwitch.Changed += OnDarkChanged;
            Grid.SetColumn(_darkSwitch, 1);
            darkRow.Children.Add(_darkSwitch);
            sp.Children.Add(darkRow);

            Grid startupRow = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            startupRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            startupRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel startupText = new StackPanel();
            startupText.Children.Add(Theme.Text("开机自启", 10.8, FontWeights.SemiBold, Theme.Ink));
            startupText.Children.Add(Theme.Meta("登录 Windows 后自动启动工具；写入当前用户启动项"));
            startupRow.Children.Add(startupText);
            _startupSwitch = new SwitchControl(_controller.StartWithWindowsEnabled);
            _startupSwitch.Changed += OnStartupChanged;
            Grid.SetColumn(_startupSwitch, 1);
            startupRow.Children.Add(_startupSwitch);
            sp.Children.Add(startupRow);

            Grid diagRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            diagRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            diagRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            diagRow.Children.Add(Theme.Meta("导出运行日志、环境与窗口列表，便于反馈问题。"));
            Button diagnostic = Theme.MakeButton("导出诊断信息", delegate { ExportDiagnostics(); }, ButtonKind.Secondary, 30);
            diagnostic.Width = 132;
            diagnostic.FontSize = 10.8;
            Grid.SetColumn(diagnostic, 1);
            diagRow.Children.Add(diagnostic);
            sp.Children.Add(diagRow);

            Grid configRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            configRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            configRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            configRow.Children.Add(Theme.Meta("备份 config.ini 与 layouts.ini，或从备份目录恢复。"));
            StackPanel configButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button exportConfig = Theme.MakeButton("导出配置", delegate { ExportConfiguration(); }, ButtonKind.Secondary, 30);
            exportConfig.Width = 94;
            exportConfig.FontSize = 10.8;
            exportConfig.Margin = new Thickness(0, 0, 7, 0);
            configButtons.Children.Add(exportConfig);
            Button importConfig = Theme.MakeButton("导入配置", delegate { ImportConfiguration(); }, ButtonKind.Secondary, 30);
            importConfig.Width = 94;
            importConfig.FontSize = 10.8;
            configButtons.Children.Add(importConfig);
            Grid.SetColumn(configButtons, 1);
            configRow.Children.Add(configButtons);
            sp.Children.Add(configRow);

            Grid updateRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            updateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            updateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            updateRow.Children.Add(Theme.Meta("检查 GitHub Release 是否有新版本；只打开下载页，不自动替换程序。"));
            Button update = Theme.MakeButton("检查更新", delegate { CheckUpdates(); }, ButtonKind.Secondary, 30);
            update.Width = 98;
            update.FontSize = 10.8;
            Grid.SetColumn(update, 1);
            updateRow.Children.Add(update);
            sp.Children.Add(updateRow);

            scroll.Content = sp;
            Grid.SetRow(scroll, 1);
            layout.Children.Add(scroll);

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            Button ok = Theme.MakeButton("完成", CloseSettings, ButtonKind.Primary, 34);
            ok.Width = 92;
            ok.FontSize = 11;
            buttons.Children.Add(ok);
            Grid.SetRow(buttons, 2);
            layout.Children.Add(buttons);

            card.Child = layout;
            overlay.Children.Add(card);
            return overlay;
        }

        private static UIElement SettingsSectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = Theme.FontUI,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.B(Theme.Ink),
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        private static UIElement SettingsShortcut(string keys, string desc, out TextBlock keyLabel)
        {
            Grid row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(148) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border keyBox = new Border
            {
                Background = Theme.B(Theme.SurfaceSoft),
                BorderBrush = Theme.B(Theme.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(7, 2, 7, 2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            keyLabel = Theme.Text(keys, 9.8, FontWeights.SemiBold, Theme.Ink);
            keyLabel.FontFamily = Theme.FontMono;
            keyBox.Child = keyLabel;
            row.Children.Add(keyBox);

            TextBlock label = Theme.Label(desc, 10.3, Theme.TextSub);
            label.TextWrapping = TextWrapping.Wrap;
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(label, 1);
            row.Children.Add(label);
            return row;
        }

        private void OpenSettings()
        {
            if (_settingsOverlay == null) return;
            ClosePreview();
            CloseAbout();
            FinishConfirm(false);
            RefreshPresetList();
            UpdateSettingsControls();
            if (_presetNameBox != null) _presetNameBox.Text = "布局 " + _arrangeCols + " 列";
            if (_presetHint != null) _presetHint.Text = "";
            _settingsOverlay.Visibility = Visibility.Visible;
            _settingsOverlay.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
        }

        private void UpdateSettingsControls()
        {
            if (_monitorCombo != null)
            {
                IReadOnlyList<MonitorInfo> monitors = _controller.GetMonitors();
                _suppressMonitorEvents = true;
                _monitorCombo.ItemsSource = monitors;
                int selected = _controller.Settings.MonitorIndex;
                if (selected < 0 || selected >= monitors.Count) selected = 0;
                _monitorCombo.SelectedIndex = monitors.Count == 0 ? -1 : selected;
                _suppressMonitorEvents = false;
            }

            if (_backgroundSlider != null)
            {
                _suppressVolumeEvents = true;
                _backgroundSlider.Value = _controller.Settings.BackgroundVolumePercent;
                if (_backgroundValue != null)
                    _backgroundValue.Text = _controller.Settings.BackgroundVolumePercent + "%";
                _suppressVolumeEvents = false;
            }

            if (_startupSwitch != null)
            {
                _suppressStartupEvents = true;
                _startupSwitch.IsOn = _controller.StartWithWindowsEnabled;
                _suppressStartupEvents = false;
            }

            if (_autoArrangeSwitch != null)
            {
                _suppressAutoArrangeEvents = true;
                _autoArrangeSwitch.IsOn = _controller.Settings.AutoArrangeNewWindows;
                _suppressAutoArrangeEvents = false;
            }

            if (_autoRestartSwitch != null)
            {
                _suppressAutoRestartEvents = true;
                _autoRestartSwitch.IsOn = _controller.Settings.AutoRestartUnresponsive;
                _suppressAutoRestartEvents = false;
            }

            if (_compactSwitch != null)
            {
                _suppressCompactEvents = true;
                _compactSwitch.IsOn = _controller.Settings.CompactMode;
                _suppressCompactEvents = false;
            }

            if (_dimSwitch != null)
            {
                _suppressDimEvents = true;
                _dimSwitch.IsOn = _controller.Settings.DimUnfocusedWindows;
                _suppressDimEvents = false;
            }

            if (_darkSwitch != null)
            {
                _suppressDarkEvents = true;
                _darkSwitch.IsOn = _controller.Settings.DarkTheme;
                _suppressDarkEvents = false;
            }

            if (_hotkeySchemeCombo != null)
            {
                _suppressHotkeyEvents = true;
                _hotkeySchemeCombo.SelectedIndex = _controller.Settings.HotkeyScheme;
                _suppressHotkeyEvents = false;
            }
            UpdateHotkeyLabels();
        }

        private void MonitorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressMonitorEvents) return;
            MonitorInfo selected = _monitorCombo == null ? null : _monitorCombo.SelectedItem as MonitorInfo;
            if (selected == null) return;
            _controller.SetMonitorIndex(selected.Index);
        }

        private void HotkeyScheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressHotkeyEvents) return;
            if (_hotkeySchemeCombo == null || _hotkeySchemeCombo.SelectedIndex < 0) return;
            _controller.SetHotkeyScheme(_hotkeySchemeCombo.SelectedIndex);
            UpdateHotkeyLabels();
        }

        private void UpdateHotkeyLabels()
        {
            int scheme = _controller.Settings.HotkeyScheme;
            string prefix = scheme == 1 ? "Ctrl+Shift+" : (scheme == 2 ? "Alt+Shift+" : "Ctrl+Alt+");
            if (_shortcutBossLabel != null) _shortcutBossLabel.Text = prefix + "H";
            if (_shortcutFocusLabel != null) _shortcutFocusLabel.Text = prefix + "1…9";
            if (_shortcutCycleLabel != null) _shortcutCycleLabel.Text = prefix + "← / →";
        }

        private void BackgroundSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int percent = (int)Math.Round(e.NewValue);
            if (_backgroundValue != null) _backgroundValue.Text = percent + "%";
            if (_suppressVolumeEvents) return;
            _controller.SetBackgroundVolume(percent);
            UpdateSoundState(_controller.SoundFocusEnabled, false);
        }

        private void OnStartupChanged(bool enabled)
        {
            if (_suppressStartupEvents) return;
            string error;
            if (!_controller.SetStartWithWindows(enabled, out error))
            {
                _logger.Log("设置开机自启失败：" + error);
                _suppressStartupEvents = true;
                if (_startupSwitch != null) _startupSwitch.IsOn = !enabled;
                _suppressStartupEvents = false;
            }
        }

        private void OnAutoArrangeChanged(bool enabled)
        {
            if (_suppressAutoArrangeEvents) return;
            _controller.SetAutoArrangeNewWindows(enabled);
        }

        private void OnAutoRestartChanged(bool enabled)
        {
            if (_suppressAutoRestartEvents) return;
            _controller.SetAutoRestartUnresponsive(enabled);
        }

        private void OnCompactChanged(bool enabled)
        {
            if (_suppressCompactEvents) return;
            _compactMode = enabled;
            _controller.SetCompactMode(enabled);
            ApplyCompactMode(true);
        }

        private void OnDimChanged(bool enabled)
        {
            if (_suppressDimEvents) return;
            _controller.SetDimUnfocusedWindows(enabled);
            ApplyWindowOpacity(_controller.ForegroundProcessId);
        }

        private void OnDarkChanged(bool enabled)
        {
            if (_suppressDarkEvents) return;
            _controller.SetDarkTheme(enabled);
            ApplyTheme(enabled);
        }

        private async void ExportDiagnostics()
        {
            using (System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog())
            {
                dialog.Title = "导出诊断信息";
                dialog.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                dialog.FileName = "阴阳师工具诊断-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                try
                {
                    StringBuilder builder = new StringBuilder();
                    builder.AppendLine("阴阳师工具诊断信息");
                    builder.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    builder.AppendLine("系统：" + Environment.OSVersion.VersionString);
                    builder.AppendLine("运行时：" + Environment.Version);
                    builder.AppendLine("管理员：" + (_isAdmin ? "是" : "否"));
                    builder.AppendLine("游戏目录：" + _controller.Settings.GameRoot);
                    builder.AppendLine("目标开数：" + _controller.Settings.TargetInstances);
                    builder.AppendLine("列数：" + _controller.Settings.Columns);
                    builder.AppendLine("显示器：" + (_controller.Settings.MonitorIndex + 1));
                    builder.AppendLine("失焦音量：" + _controller.Settings.BackgroundVolumePercent + "%");
                    builder.AppendLine("声音跟随：" + (_controller.SoundFocusEnabled ? "开启" : "关闭"));
                    builder.AppendLine("隐藏窗口：" + _controller.HiddenWindowCount);
                    builder.AppendLine();

                    IReadOnlyList<GameWindowInfo> windows = await _controller.GetWindowsAsync();
                    builder.AppendLine("可见游戏窗口：" + windows.Count + " 个");
                    for (int i = 0; i < windows.Count; i++)
                    {
                        builder.AppendLine(string.Format("  {0}. PID {1} · {2} · ({3},{4}) {5}×{6}",
                            i + 1, windows[i].ProcessId, windows[i].Title,
                            windows[i].Bounds.Left, windows[i].Bounds.Top,
                            windows[i].Bounds.Width, windows[i].Bounds.Height));
                    }
                    builder.AppendLine();
                    builder.AppendLine("运行日志：");
                    builder.AppendLine(txtLog == null ? "" : txtLog.Text);

                    File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(false));
                    _logger.Log("诊断信息已导出：" + dialog.FileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError("导出诊断信息失败", ex);
                }
            }
        }

        private void ExportConfiguration()
        {
            using (System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择配置备份目录（会写入 config.ini / layouts.ini）";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string error;
                if (!_controller.ExportConfiguration(dialog.SelectedPath, out error))
                {
                    _logger.Log("导出配置失败：" + error);
                    return;
                }
                _logger.Log("配置已导出：" + dialog.SelectedPath);
            }
        }

        private async void ImportConfiguration()
        {
            using (System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择包含 config.ini 的备份目录";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string folder = dialog.SelectedPath;
                CloseSettings();
                bool confirmed = await ConfirmAsync(
                    "导入配置",
                    "将用备份目录中的 config.ini / layouts.ini 覆盖当前配置。导入后需要重启工具才能生效，是否继续？",
                    "确认导入");
                if (!confirmed) return;

                string error;
                if (!_controller.ImportConfiguration(folder, out error))
                {
                    _logger.Log("导入配置失败：" + error);
                    return;
                }
                if (txtOpenNum != null) txtOpenNum.Text = _controller.Settings.TargetInstances.ToString();
                SetArrangeCols(_controller.Settings.Columns, false);
                _logger.Log("配置已导入：" + folder + "，请重启工具后生效。");
            }
        }

        private async void CheckUpdates()
        {
            _logger.Log("正在检查更新…");
            try
            {
                UpdateInfo info = await Task.Run(delegate { return UpdateChecker.Check(); });
                if (!string.IsNullOrEmpty(info.Error))
                {
                    _logger.Log(info.Error);
                    return;
                }
                if (!info.HasUpdate)
                {
                    _logger.Log("已是最新版本（v" + info.CurrentVersion + "）。");
                    return;
                }

                CloseSettings();
                if (!string.IsNullOrEmpty(info.DownloadUrl))
                {
                    bool update = await ConfirmAsync(
                        "发现新版本",
                        "当前 v" + info.CurrentVersion + "，最新 " + info.LatestVersion +
                        "。\n\n将下载并自动替换当前程序，替换前保留 .bak 备份；工具会关闭并重新启动。是否继续？",
                        "下载并更新");
                    if (!update) return;

                    _logger.Log("正在下载更新…");
                    string file = null;
                    string error = null;
                    bool downloaded = await Task.Run(delegate
                    {
                        string f;
                        string e;
                        bool ok = UpdateInstaller.Download(info.DownloadUrl, out f, out e);
                        file = f;
                        error = e;
                        return ok;
                    });
                    if (!downloaded)
                    {
                        _logger.Log("下载更新失败：" + error + "，已改为打开下载页。");
                        try { System.Diagnostics.Process.Start(info.ReleaseUrl); } catch { }
                        return;
                    }

                    if (!UpdateInstaller.ScheduleReplace(file, out error))
                    {
                        _logger.Log("安排自动替换失败：" + error + "，已改为打开下载页。");
                        try { System.Diagnostics.Process.Start(info.ReleaseUrl); } catch { }
                        return;
                    }

                    _logger.Log("更新已下载，工具即将关闭并替换后重启。");
                    Close();
                    return;
                }

                bool open = await ConfirmAsync(
                    "发现新版本",
                    "当前 v" + info.CurrentVersion + "，最新 " + info.LatestVersion + "。是否打开 GitHub 下载页？",
                    "打开下载页");
                if (!open) return;
                System.Diagnostics.Process.Start(info.ReleaseUrl);
                _logger.Log("已打开更新下载页：" + info.ReleaseUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError("检查更新失败", ex);
            }
        }

        private void CloseSettings()
        {
            if (_settingsOverlay == null) return;
            _settingsOverlay.Visibility = Visibility.Collapsed;
        }

        private void RefreshPresetList()
        {
            if (_presetListPanel == null) return;
            _presetListPanel.Children.Clear();
            IReadOnlyList<LayoutPreset> presets = _controller.GetLayoutPresets();
            if (presets.Count == 0)
            {
                _presetListPanel.Children.Add(Theme.Meta("还没有布局预设。输入名称后点「保存当前列数」。"));
                return;
            }

            for (int i = 0; i < presets.Count; i++)
            {
                LayoutPreset preset = presets[i];
                Grid row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                TextBlock name = Theme.Text(preset.Name, 10.8, FontWeights.SemiBold, Theme.Ink);
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                row.Children.Add(name);

                TextBlock columns = Theme.Meta(preset.Columns + " 列 · 屏 " + (preset.MonitorIndex + 1));
                columns.Margin = new Thickness(8, 0, 8, 0);
                Grid.SetColumn(columns, 1);
                row.Children.Add(columns);

                Button apply = Theme.MakeButton("应用", delegate { ApplyPreset(preset); }, ButtonKind.Link, 24);
                apply.Width = 56;
                apply.FontSize = 10.4;
                apply.Padding = new Thickness(0);
                Grid.SetColumn(apply, 2);
                row.Children.Add(apply);

                Button delete = Theme.MakeButton("删除", delegate { DeletePreset(preset); }, ButtonKind.Link, 24);
                delete.Width = 56;
                delete.FontSize = 10.4;
                delete.Padding = new Thickness(0);
                delete.Foreground = Theme.B(Theme.TextMuted);
                delete.Margin = new Thickness(8, 0, 0, 0);
                Grid.SetColumn(delete, 3);
                row.Children.Add(delete);

                _presetListPanel.Children.Add(row);
                _presetListPanel.Children.Add(Theme.Hairline());
            }
        }

        private void SavePreset()
        {
            if (_presetNameBox == null) return;
            string name = _presetNameBox.Text.Trim();
            if (name.Length == 0) name = "布局 " + _arrangeCols + " 列";

            string error;
            if (!_controller.SaveLayoutPreset(name, _arrangeCols, _controller.Settings.MonitorIndex, out error))
            {
                SetPresetHint(error, true);
                return;
            }
            SetPresetHint("已保存：" + name + "（" + _arrangeCols + " 列，显示器 " +
                (_controller.Settings.MonitorIndex + 1) + "）", false);
            RefreshPresetList();
        }

        private void DeletePreset(LayoutPreset preset)
        {
            if (preset == null) return;
            string error;
            if (!_controller.DeleteLayoutPreset(preset.Name, out error))
            {
                SetPresetHint(error, true);
                return;
            }
            SetPresetHint("已删除：" + preset.Name, false);
            RefreshPresetList();
        }

        private async void ApplyPreset(LayoutPreset preset)
        {
            if (preset == null) return;
            if (_controller.IsBusy)
            {
                SetPresetHint("当前有操作正在进行，请稍后再应用。", true);
                return;
            }

            CloseSettings();
            _controller.SetMonitorIndex(preset.MonitorIndex);
            SetArrangeCols(preset.Columns, true);
            try
            {
                _logger.Log("应用布局预设：" + preset.Name + "（" + preset.Columns + " 列，显示器 " +
                    (preset.MonitorIndex + 1) + "）。");
                await _controller.ArrangeAsync(preset.Columns);
                await RefreshListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("应用布局预设失败", ex);
            }
        }

        private void SetPresetHint(string text, bool error)
        {
            if (_presetHint == null) return;
            _presetHint.Text = text ?? "";
            _presetHint.Foreground = Theme.B(error ? Theme.Accent : Theme.Green);
        }

        // ==================== 资源面板 ====================
        private Grid BuildResourcesOverlay()
        {
            Grid overlay = new Grid { Visibility = Visibility.Collapsed, Opacity = 1 };
            Border dim = new Border { Background = Theme.BA(0xB8, Theme.Ink), CornerRadius = new CornerRadius(12) };
            dim.MouseLeftButtonDown += delegate { CloseResources(); };
            overlay.Children.Add(dim);

            Border card = new Border
            {
                Width = 640,
                MaxHeight = 520,
                Background = Theme.B(Theme.Surface),
                BorderBrush = Theme.B(Theme.LineStrong),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(24, 20, 24, 18),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            card.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel titleStack = new StackPanel();
            titleStack.Children.Add(Theme.Text("实例资源", 17, FontWeights.Bold, Theme.Ink));
            titleStack.Children.Add(Theme.Meta("内存、CPU 累计时间与运行时长；打开时读取一次"));
            head.Children.Add(titleStack);
            Button close = Theme.MakeChromeButton("\uE8BB", false, CloseResources, "关闭资源面板");
            close.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(close, 1);
            head.Children.Add(close);
            Grid.SetRow(head, 0);
            layout.Children.Add(head);

            _resourcesSummary = Theme.Meta("正在读取…");
            _resourcesSummary.Margin = new Thickness(0, 8, 0, 8);
            Grid.SetRow(_resourcesSummary, 1);
            layout.Children.Add(_resourcesSummary);

            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _resourcesPanel = new StackPanel();
            scroll.Content = _resourcesPanel;
            Grid.SetRow(scroll, 2);
            layout.Children.Add(scroll);

            StackPanel footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            Button refresh = Theme.MakeButton("刷新", async delegate { await RefreshResourcesAsync(); }, ButtonKind.Secondary, 32);
            refresh.Width = 78;
            refresh.FontSize = 10.8;
            refresh.Margin = new Thickness(0, 0, 8, 0);
            footer.Children.Add(refresh);
            Button ok = Theme.MakeButton("关闭", CloseResources, ButtonKind.Primary, 32);
            ok.Width = 78;
            ok.FontSize = 10.8;
            footer.Children.Add(ok);
            Grid.SetRow(footer, 3);
            layout.Children.Add(footer);

            card.Child = layout;
            overlay.Children.Add(card);
            return overlay;
        }

        private async void OpenResources()
        {
            if (_resourcesOverlay == null) return;
            ClosePreview();
            CloseAbout();
            CloseSettings();
            FinishConfirm(false);
            _resourcesOverlay.Visibility = Visibility.Visible;
            _resourcesOverlay.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130)));
            await RefreshResourcesAsync();
        }

        private void CloseResources()
        {
            if (_resourcesOverlay == null) return;
            _resourcesOverlay.Visibility = Visibility.Collapsed;
        }

        private async Task RefreshResourcesAsync()
        {
            if (_resourcesPanel == null || _resourcesOverlay == null ||
                _resourcesOverlay.Visibility != Visibility.Visible) return;

            _resourcesSummary.Text = "正在读取…";
            _resourcesPanel.Children.Clear();
            try
            {
                IReadOnlyList<GameWindowInfo> windows = await _controller.GetWindowsAsync();
                IReadOnlyList<ProcessResourceInfo> resources = await Task.Run(delegate
                {
                    return ProcessResourceReader.Read(windows);
                });

                _resourcesSummary.Text = "共 " + resources.Count + " 个可见实例 · CPU 为进程累计时间";
                _resourcesPanel.Children.Add(ResourceHeader());
                for (int i = 0; i < resources.Count; i++)
                    _resourcesPanel.Children.Add(ResourceRow(i + 1, resources[i]));
                if (resources.Count == 0)
                    _resourcesPanel.Children.Add(Theme.Meta("当前没有可见的游戏实例。"));
            }
            catch (Exception ex)
            {
                _resourcesSummary.Text = "读取失败：" + ex.Message;
            }
        }

        private static UIElement ResourceHeader()
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            AddResourceCell(g, "#", 0, true);
            AddResourceCell(g, "PID", 1, true);
            AddResourceCell(g, "窗口标题", 2, true);
            AddResourceCell(g, "内存", 3, true);
            AddResourceCell(g, "CPU 累计", 4, true);
            AddResourceCell(g, "运行时长", 5, true);
            return g;
        }

        private static UIElement ResourceRow(int index, ProcessResourceInfo info)
        {
            Border row = new Border
            {
                Background = Theme.B(Theme.SurfaceSoft),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(4, 5, 4, 5),
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });

            AddResourceCell(g, index.ToString("00"), 0, false);
            AddResourceCell(g, info.ProcessId.ToString(), 1, false);
            AddResourceCell(g, info.Title, 2, false);
            AddResourceCell(g, info.Available ? (info.WorkingSetBytes / 1024d / 1024d).ToString("0") + " MB" : "已退出", 3, false);
            AddResourceCell(g, info.Available ? FormatDuration(info.CpuTime) : "—", 4, false);
            AddResourceCell(g, info.Available && info.StartTime.HasValue
                ? FormatDuration(DateTime.Now - info.StartTime.Value) : "—", 5, false);
            row.Child = g;
            return row;
        }

        private static void AddResourceCell(Grid g, string text, int column, bool header)
        {
            TextBlock label = Theme.Text(text ?? "", header ? 9.2 : 9.8,
                header ? FontWeights.SemiBold : FontWeights.Normal,
                header ? Theme.TextMuted : Theme.Ink);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.ToolTip = text;
            Grid.SetColumn(label, column);
            g.Children.Add(label);
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalHours >= 24) return ((int)span.TotalDays) + "天 " + span.Hours + "小时";
            if (span.TotalHours >= 1) return ((int)span.TotalHours) + ":" + span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
            return span.Minutes + ":" + span.Seconds.ToString("00");
        }

        // ==================== 实例标签弹层 ====================
        private Grid BuildLabelOverlay()
        {
            Grid overlay = new Grid { Visibility = Visibility.Collapsed, Opacity = 1 };
            Border dim = new Border { Background = Theme.BA(0xB8, Theme.Ink), CornerRadius = new CornerRadius(12) };
            dim.MouseLeftButtonDown += delegate { FinishLabel(null); };
            overlay.Children.Add(dim);

            Border card = new Border
            {
                Width = 450,
                Background = Theme.B(Theme.Surface),
                BorderBrush = Theme.B(Theme.LineStrong),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(24, 20, 24, 18),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            card.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };

            StackPanel sp = new StackPanel();
            sp.Children.Add(Theme.Text("设置实例标签", 16, FontWeights.Bold, Theme.Ink));
            _labelWindowTitle = Theme.Meta("");
            _labelWindowTitle.TextWrapping = TextWrapping.Wrap;
            _labelWindowTitle.Margin = new Thickness(0, 6, 0, 10);
            sp.Children.Add(_labelWindowTitle);

            _labelTextBox = Theme.MakeTextBox("", 392, false);
            _labelTextBox.MaxLength = 20;
            _labelTextBox.ToolTip = "最多 20 个字符；留空并保存可清除标签";
            sp.Children.Add(_labelTextBox);
            TextBlock hint = Theme.Meta("标签按窗口标题保存，用于区分主号/小号；留空保存即清除。");
            hint.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(hint);

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            Button cancel = Theme.MakeButton("取消", delegate { FinishLabel(null); }, ButtonKind.Secondary, 32);
            cancel.Width = 78;
            cancel.FontSize = 10.8;
            cancel.Margin = new Thickness(0, 0, 8, 0);
            buttons.Children.Add(cancel);
            Button save = Theme.MakeButton("保存", delegate { FinishLabel(_labelTextBox.Text.Trim()); }, ButtonKind.Primary, 32);
            save.Width = 78;
            save.FontSize = 10.8;
            buttons.Children.Add(save);
            sp.Children.Add(buttons);

            card.Child = sp;
            overlay.Children.Add(card);
            return overlay;
        }

        private Task<string> EditLabelAsync(string windowTitle, string currentLabel)
        {
            if (_labelOverlay == null || _labelResult != null) return Task.FromResult<string>(null);
            _labelWindowTitle.Text = windowTitle ?? "";
            _labelTextBox.Text = currentLabel ?? "";
            _labelResult = new TaskCompletionSource<string>();
            _labelOverlay.Visibility = Visibility.Visible;
            _labelOverlay.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130)));
            _labelTextBox.Focus();
            _labelTextBox.SelectAll();
            return _labelResult.Task;
        }

        private void FinishLabel(string value)
        {
            if (_labelOverlay == null) return;
            _labelOverlay.Visibility = Visibility.Collapsed;
            TaskCompletionSource<string> source = _labelResult;
            _labelResult = null;
            if (source != null) source.TrySetResult(value);
        }

        private void CancelLabel()
        {
            FinishLabel(null);
        }

        private async void EditLabelFromMenu(InstanceRow info)
        {
            if (info == null) return;
            string label = await EditLabelAsync(info.Title, info.CustomLabel);
            if (label == null) return;
            _controller.SetInstanceLabel(info.Title, label);
            await RefreshListAsync();
        }

        // ==================== 缩略图预览 ====================
        private Grid BuildPreviewOverlay()
        {
            Grid overlay = new Grid { Visibility = Visibility.Collapsed, Opacity = 1 };
            Border dim = new Border { Background = Theme.BA(0xB8, Theme.Ink), CornerRadius = new CornerRadius(12) };
            dim.MouseLeftButtonDown += delegate { ClosePreview(); };
            overlay.Children.Add(dim);

            Border card = new Border
            {
                Width = 860,
                MaxHeight = 640,
                Background = Theme.B(Theme.Surface),
                BorderBrush = Theme.B(Theme.LineStrong),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(22, 18, 22, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            card.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel titleStack = new StackPanel();
            titleStack.Children.Add(Theme.Text("实例缩略图", 17, FontWeights.Bold, Theme.Ink));
            titleStack.Children.Add(Theme.Meta("DWM 实时预览，自动填满预览框；点击缩略图可聚焦"));
            head.Children.Add(titleStack);
            Button close = Theme.MakeChromeButton("\uE8BB", false, ClosePreview, "关闭预览");
            close.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(close, 1);
            head.Children.Add(close);
            Grid.SetRow(head, 0);
            layout.Children.Add(head);

            _previewSummary = Theme.Meta("正在抓取…");
            _previewSummary.Margin = new Thickness(0, 8, 0, 8);
            Grid.SetRow(_previewSummary, 1);
            layout.Children.Add(_previewSummary);

            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _previewPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            scroll.Content = _previewPanel;
            Grid.SetRow(scroll, 2);
            layout.Children.Add(scroll);

            StackPanel footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            Button refresh = Theme.MakeButton("重新加载", async delegate { await RefreshPreviewAsync(); }, ButtonKind.Secondary, 32);
            refresh.Width = 92;
            refresh.FontSize = 10.8;
            refresh.Margin = new Thickness(0, 0, 8, 0);
            footer.Children.Add(refresh);
            Button ok = Theme.MakeButton("关闭", ClosePreview, ButtonKind.Primary, 32);
            ok.Width = 78;
            ok.FontSize = 10.8;
            footer.Children.Add(ok);
            Grid.SetRow(footer, 3);
            layout.Children.Add(footer);

            card.Child = layout;
            overlay.Children.Add(card);
            return overlay;
        }

        private async void OpenPreview()
        {
            if (_previewOverlay == null) return;
            CloseAbout();
            CloseSettings();
            CloseResources();
            FinishConfirm(false);
            _previewOverlay.Visibility = Visibility.Visible;
            _previewOverlay.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130)));
            await RefreshPreviewAsync();
        }

        private void ClosePreview()
        {
            UnregisterPreviewThumbnails();
            _previewTargets.Clear();
            if (_previewOverlay == null) return;
            _previewOverlay.Visibility = Visibility.Collapsed;
            if (_previewPanel != null) _previewPanel.Children.Clear();
        }

        private async Task RefreshPreviewAsync()
        {
            if (_previewPanel == null || _previewOverlay == null ||
                _previewOverlay.Visibility != Visibility.Visible) return;

            UnregisterPreviewThumbnails();
            _previewTargets.Clear();
            _previewSummary.Text = "正在加载实时预览…";
            _previewPanel.Children.Clear();
            try
            {
                IReadOnlyList<GameWindowInfo> windows = await _controller.GetWindowsAsync();
                _previewSummary.Text = "共 " + windows.Count + " 个可见实例 · DWM 实时画面";
                for (int i = 0; i < windows.Count; i++)
                {
                    GameWindowInfo window = windows[i];

                    Border card = new Border
                    {
                        Width = 195,
                        Background = Theme.B(Theme.SurfaceSoft),
                        BorderBrush = Theme.B(Theme.Line),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(5),
                        Margin = new Thickness(0, 0, 8, 8),
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    StackPanel content = new StackPanel();

                    Grid imageHost = new Grid
                    {
                        Width = 183,
                        Height = 103,
                        Background = Theme.B(Theme.Ink),
                        ClipToBounds = true
                    };
                    TextBlock fallback = Theme.Meta("DWM 预览不可用");
                    fallback.HorizontalAlignment = HorizontalAlignment.Center;
                    fallback.VerticalAlignment = VerticalAlignment.Center;
                    fallback.IsHitTestVisible = false;
                    imageHost.Children.Add(fallback);
                    content.Children.Add(imageHost);

                    TextBlock title = Theme.Text(window.Title, 10.2, FontWeights.SemiBold, Theme.Ink);
                    title.TextTrimming = TextTrimming.CharacterEllipsis;
                    title.ToolTip = window.Title;
                    title.Margin = new Thickness(0, 5, 0, 0);
                    content.Children.Add(title);
                    TextBlock meta = Theme.Meta("PID " + window.ProcessId + " · 点击聚焦");
                    meta.Margin = new Thickness(0, 2, 0, 2);
                    content.Children.Add(meta);
                    card.Child = content;

                    _previewTargets.Add(new PreviewThumbnailTarget
                    {
                        Source = window.Handle,
                        Frame = imageHost
                    });

                    GameWindowInfo captured = window;
                    card.MouseLeftButtonDown += delegate
                    {
                        if (_controller.FocusWindow(captured.Handle)) ClosePreview();
                    };
                    _previewPanel.Children.Add(card);
                }
                if (windows.Count == 0)
                    _previewPanel.Children.Add(Theme.Meta("当前没有可见的游戏实例。"));

                System.Windows.Threading.DispatcherOperation registration =
                    Dispatcher.BeginInvoke(new Action(RegisterPreviewThumbnails),
                        System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                _previewSummary.Text = "加载失败：" + ex.Message;
            }
        }

        private void RegisterPreviewThumbnails()
        {
            UnregisterPreviewThumbnails();
            if (_previewOverlay == null || _previewOverlay.Visibility != Visibility.Visible) return;

            IntPtr destination = new WindowInteropHelper(this).Handle;
            FrameworkElement root = Content as FrameworkElement;
            if (destination == IntPtr.Zero || root == null) return;

            PresentationSource source = PresentationSource.FromVisual(this);
            double scaleX = 1.0;
            double scaleY = 1.0;
            if (source != null && source.CompositionTarget != null)
            {
                scaleX = source.CompositionTarget.TransformToDevice.M11;
                scaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            for (int i = 0; i < _previewTargets.Count; i++)
            {
                PreviewThumbnailTarget target = _previewTargets[i];
                if (target.Source == IntPtr.Zero || target.Frame == null) continue;
                if (!NativeMethods.IsWindow(target.Source)) continue;

                Point position = target.Frame.TranslatePoint(new Point(0, 0), root);
                int width = (int)Math.Round(target.Frame.ActualWidth * scaleX);
                int height = (int)Math.Round(target.Frame.ActualHeight * scaleY);
                if (width <= 0 || height <= 0) continue;

                IntPtr thumbnail;
                int hr = NativeMethods.DwmRegisterThumbnail(destination, target.Source, out thumbnail);
                if (hr != 0 || thumbnail == IntPtr.Zero) continue;

                NativeMethods.DWM_THUMBNAIL_PROPERTIES props = new NativeMethods.DWM_THUMBNAIL_PROPERTIES();
                props.dwFlags = NativeMethods.DWM_TNP_RECTDESTINATION |
                    NativeMethods.DWM_TNP_RECTSOURCE |
                    NativeMethods.DWM_TNP_OPACITY |
                    NativeMethods.DWM_TNP_VISIBLE |
                    NativeMethods.DWM_TNP_SOURCECLIENTAREAONLY;
                int left = (int)Math.Round(position.X * scaleX);
                int top = (int)Math.Round(position.Y * scaleY);
                props.rcDestination = new NativeMethods.RECT
                {
                    Left = left,
                    Top = top,
                    Right = left + width,
                    Bottom = top + height
                };
                props.rcSource = new NativeMethods.RECT { Left = 0, Top = 0, Right = width, Bottom = height };
                props.opacity = 255;
                props.fVisible = true;
                props.fSourceClientAreaOnly = true;

                NativeMethods.SIZE sourceSize;
                NativeMethods.RECT clientRect;
                if (NativeMethods.GetClientRect(target.Source, out clientRect) &&
                    clientRect.Width > 0 && clientRect.Height > 0)
                {
                    // 非 DPI-aware 的游戏窗口会被系统按当前 DPI 虚拟化；
                    // DWM 源坐标仍以虚拟化尺寸返回，这里收回到真实像素，避免只显示左上角。
                    uint sourceDpi = NativeMethods.GetDpiForWindow(target.Source);
                    double appDpi = 96.0 * scaleX;
                    double factor = sourceDpi > 0 && appDpi > 0 && sourceDpi < appDpi
                        ? sourceDpi / appDpi
                        : 1.0;
                    int sourceWidth = (int)Math.Round(clientRect.Width * factor);
                    int sourceHeight = (int)Math.Round(clientRect.Height * factor);
                    props.rcSource = CropThumbnailSource(
                        Math.Max(1, sourceWidth), Math.Max(1, sourceHeight), width, height);
                }
                else if (NativeMethods.DwmQueryThumbnailSourceSize(thumbnail, out sourceSize) == 0 &&
                    sourceSize.cx > 0 && sourceSize.cy > 0)
                {
                    props.rcSource = CropThumbnailSource(sourceSize.cx, sourceSize.cy, width, height);
                }

                if (NativeMethods.DwmUpdateThumbnailProperties(thumbnail, ref props) == 0)
                    _previewThumbnails.Add(thumbnail);
                else
                    NativeMethods.DwmUnregisterThumbnail(thumbnail);
            }
        }

        private void UnregisterPreviewThumbnails()
        {
            for (int i = 0; i < _previewThumbnails.Count; i++)
            {
                try { NativeMethods.DwmUnregisterThumbnail(_previewThumbnails[i]); }
                catch { }
            }
            _previewThumbnails.Clear();
        }

        private static NativeMethods.RECT CropThumbnailSource(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            double targetRatio = targetWidth / (double)targetHeight;
            double sourceRatio = sourceWidth / (double)sourceHeight;
            NativeMethods.RECT rect = new NativeMethods.RECT
            {
                Left = 0,
                Top = 0,
                Right = sourceWidth,
                Bottom = sourceHeight
            };

            if (sourceRatio > targetRatio)
            {
                int cropWidth = (int)Math.Round(sourceHeight * targetRatio);
                int left = (sourceWidth - cropWidth) / 2;
                rect.Left = left;
                rect.Right = left + cropWidth;
            }
            else if (sourceRatio < targetRatio)
            {
                int cropHeight = (int)Math.Round(sourceWidth / targetRatio);
                int top = (sourceHeight - cropHeight) / 2;
                rect.Top = top;
                rect.Bottom = top + cropHeight;
            }
            return rect;
        }

        private static UIElement AboutRow(string index, string title, string desc)
        {
            Grid g = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock indexLabel = new TextBlock
            {
                Text = index,
                FontFamily = Theme.FontSerif,
                FontSize = 10.2,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.B(Theme.Gold),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };
            Grid.SetColumn(indexLabel, 0);
            g.Children.Add(indexLabel);

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
    }
}

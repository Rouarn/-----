using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using OnmyojiTools.App;
using OnmyojiTools.Core;
using OnmyojiTools.Diagnostics;
using Path = System.IO.Path;

namespace OnmyojiTools.Ui
{
    /// <summary>
    /// 主窗口：和纸 · 墨 · 朱印控制台。只负责视图状态和用户交互；
    /// 所有进程/窗口/音频操作都通过 AppController 完成。
    /// </summary>
    internal sealed partial class MainWindow : Window
    {
        private readonly AppController _controller;
        private readonly AppLogger _logger;
        private readonly bool _isAdmin = IsAdministrator();

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
        private Button _refreshIcon;
        private StackPanel _instancePanel;
        private ScrollViewer _instanceScroll;
        private Border _emptyState;
        private Border _openStepper;
        private Border _colWrap;
        private SwitchControl swSound;
        private Grid _aboutOverlay;
        private Grid _confirmOverlay;
        private TextBlock _confirmTitle;
        private TextBlock _confirmMessage;
        private Button _confirmOkButton;
        private TaskCompletionSource<bool> _confirmResult;
        private Border _shell;
        private Border _busyPanel;
        private TextBlock _busyText;

        private Border[] _colSegments = new Border[4];
        private TextBlock[] _colLabels = new TextBlock[4];
        private int _arrangeCols;
        private bool _closing;
        private bool _suppressSoundEvents;

        internal MainWindow(AppController controller, AppLogger logger)
        {
            _controller = controller;
            _logger = logger;
            _arrangeCols = controller.Settings.Columns;

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

            _logger.Attach(AppendLog);
            txtRoot.Text = controller.Settings.GameRoot;
            txtOpenNum.Text = controller.Settings.TargetInstances.ToString();
            SetArrangeCols(controller.Settings.Columns, false);
            UpdateSoundState(controller.Settings.SoundFocusEnabled, false);
            UpdateBusyState();

            _logger.Log("控制台已就绪。");
            _logger.Log("游戏目录：" + controller.Settings.GameRoot);
            if (!_isAdmin) _logger.Log("提示：当前为普通权限，窗口与音量操作可能失败，建议以管理员身份运行。");

            _controller.StateChanged += Controller_StateChanged;

            Loaded += Window_Loaded;
            Closing += Window_Closing;
            PreviewKeyDown += Window_PreviewKeyDown;
        }

        // ==================== 生命周期 ====================
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Activate();
            Topmost = false;

            if (_shell != null)
            {
                DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
                fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                _shell.BeginAnimation(UIElement.OpacityProperty, fade);
            }

            _controller.Start();
            UpdateRunningLabel();
            RunRefresh();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_closing) return;
            _closing = true;

            FinishConfirm(false);
            CloseAbout();

            int target;
            if (txtOpenNum != null &&
                InputValidation.TryParseInt(txtOpenNum.Text, AppSettings.MinInstances, AppSettings.MaxInstances, out target))
            {
                _controller.UpdateTargetInstances(target);
            }

            _controller.StateChanged -= Controller_StateChanged;
            _controller.CancelCurrentOperation();

            try { _controller.Dispose(); }
            catch (Exception ex) { _logger.LogError("关闭时清理资源失败", ex); }

            _logger.Detach();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_confirmOverlay != null && _confirmOverlay.Visibility == Visibility.Visible)
                {
                    FinishConfirm(false);
                    e.Handled = true;
                }
                else if (_aboutOverlay != null && _aboutOverlay.Visibility == Visibility.Visible)
                {
                    CloseAbout();
                    e.Handled = true;
                }
                else if (_controller.IsBusy)
                {
                    CancelBusy();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.F5 && !_controller.IsBusy)
            {
                BtnRefresh_Click();
                e.Handled = true;
            }
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

            UIElement header = BuildHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Grid body = new Grid { Margin = new Thickness(22, 18, 22, 14) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(356) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel left = new StackPanel();
            Border cardLaunch = BuildLaunchCard(_controller.Settings.TargetInstances);
            cardLaunch.Margin = new Thickness(0, 0, 0, 14);
            left.Children.Add(cardLaunch);
            Border cardArrange = BuildArrangeCard(_controller.Settings.Columns);
            cardArrange.Margin = new Thickness(0, 0, 0, 14);
            left.Children.Add(cardArrange);
            left.Children.Add(BuildSoundCard(_controller.Settings.SoundFocusEnabled));
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

            UIElement footer = BuildFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            _aboutOverlay = BuildAboutOverlay();
            Grid.SetRowSpan(_aboutOverlay, 3);
            root.Children.Add(_aboutOverlay);

            _confirmOverlay = BuildConfirmOverlay();
            Grid.SetRowSpan(_confirmOverlay, 3);
            root.Children.Add(_confirmOverlay);

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

            // 操作进行中的提示与取消按钮
            _busyText = new TextBlock
            {
                Text = "",
                FontFamily = Theme.FontUI,
                FontSize = 10.5,
                Foreground = Theme.B(Theme.Accent),
                VerticalAlignment = VerticalAlignment.Center
            };
            Button busyCancel = Theme.MakeButton("取消", CancelBusy, ButtonKind.Link, 22);
            busyCancel.FontSize = 10.5;
            busyCancel.Margin = new Thickness(6, 0, 0, 0);
            StackPanel busyContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            busyContent.Children.Add(_busyText);
            busyContent.Children.Add(busyCancel);
            _busyPanel = new Border
            {
                Background = Theme.B(Theme.AccentSoft),
                BorderBrush = Theme.B(Theme.AccentLine),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(10, 2, 4, 2),
                Margin = new Thickness(0, 0, 12, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Child = busyContent
            };
            Grid.SetColumn(_busyPanel, 1);
            g.Children.Add(_busyPanel);

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
                Child = status,
                ToolTip = "当前正在运行的阴阳师进程数"
            };
            Grid.SetColumn(statusBox, 2);
            g.Children.Add(statusBox);

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
            chrome.Children.Add(Theme.MakeChromeButton("\uE921", false, delegate { WindowState = WindowState.Minimized; }, "最小化"));
            chrome.Children.Add(Theme.MakeChromeButton("\uE8BB", true, delegate { Close(); }, "关闭"));
            Grid.SetColumn(chrome, 4);
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
            right.Children.Add(Theme.Dot(5, _isAdmin ? Theme.Green : Theme.Amber));
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

        // ==================== 状态更新 ====================
        private void Controller_StateChanged(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(UpdateBusyState));
                return;
            }
            UpdateBusyState();
        }

        private void UpdateBusyState()
        {
            bool busy = _controller.IsBusy;
            if (_busyPanel != null)
            {
                _busyPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
                if (_busyText != null) _busyText.Text = BusyLabel(_controller.CurrentOperation);
            }

            if (btnOpen != null) btnOpen.IsEnabled = !busy;
            if (btnArrange != null) btnArrange.IsEnabled = !busy;
            if (btnCloseAll != null) btnCloseAll.IsEnabled = !busy;
            if (btnRefresh != null) btnRefresh.IsEnabled = !busy;
            if (btnBrowse != null) btnBrowse.IsEnabled = !busy;
            if (_refreshIcon != null) _refreshIcon.IsEnabled = !busy;
            if (_openStepper != null) _openStepper.IsEnabled = !busy;
            if (_colWrap != null) _colWrap.IsEnabled = !busy;
        }

        private static string BusyLabel(string operation)
        {
            if (operation == "launch") return "正在多开…";
            if (operation == "arrange") return "正在排列窗口…";
            if (operation == "close") return "正在关闭窗口…";
            return "正在处理…";
        }

        private void CancelBusy()
        {
            if (!_controller.IsBusy) return;
            _controller.CancelCurrentOperation();
            _logger.Log("正在取消当前操作…");
        }

        private void UpdateRunningLabel()
        {
            int count = _controller.GetRunningCount();
            if (lblRunning != null) lblRunning.Text = count.ToString();
            if (_statusDot != null) _statusDot.Fill = Theme.B(count > 0 ? Theme.Green : Theme.TextMuted);
        }

        private void UpdateSoundState(bool enabled, bool updateSwitch)
        {
            if (updateSwitch && swSound != null)
            {
                _suppressSoundEvents = true;
                swSound.IsOn = enabled;
                _suppressSoundEvents = false;
            }
            if (lblSoundState != null)
            {
                lblSoundState.Text = enabled ? "监控中" : "已暂停";
                lblSoundState.Foreground = Theme.B(enabled ? Theme.Green : Theme.TextMuted);
            }
            if (_soundDot != null) _soundDot.Fill = Theme.B(enabled ? Theme.Green : Theme.TextMuted);
        }

        private void AppendLog(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                try { Dispatcher.BeginInvoke(new Action<string>(AppendLog), message); }
                catch { }
                return;
            }
            if (txtLog == null) return;
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
            txtLog.ScrollToEnd();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is ButtonBase || source is SwitchControl || source is TextBox) return;
                source = VisualTreeHelper.GetParent(source);
            }
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        // ==================== 操作入口 ====================
        private async void BtnOpen_Click()
        {
            if (_controller.IsBusy) return;
            int target;
            if (!InputValidation.TryParseInt(txtOpenNum.Text, AppSettings.MinInstances, AppSettings.MaxInstances, out target))
            {
                _logger.Log(string.Format("输入无效：请输入 {0}-{1} 之间的整数。", AppSettings.MinInstances, AppSettings.MaxInstances));
                txtOpenNum.Focus();
                txtOpenNum.SelectAll();
                return;
            }

            try
            {
                _controller.UpdateTargetInstances(target);
                await _controller.LaunchAsync(target);
                UpdateRunningLabel();
                await RefreshListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("多开操作失败", ex);
            }
        }

        private async void BtnArrange_Click()
        {
            if (_controller.IsBusy) return;
            try
            {
                await _controller.ArrangeAsync(_arrangeCols);
                UpdateRunningLabel();
                await RefreshListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("窗口排列失败", ex);
            }
        }

        private async void BtnCloseAll_Click()
        {
            if (_controller.IsBusy) return;

            bool confirmed = await ConfirmAsync(
                "关闭所有游戏窗口",
                "将向所有阴阳师窗口发送退出请求，并自动点击退出确认；3 秒内未退出会强制结束进程。是否继续？",
                "确认关闭");
            if (!confirmed) return;

            try
            {
                await _controller.CloseAllAsync();
                UpdateRunningLabel();
                await RefreshListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("关闭所有窗口失败", ex);
            }
        }

        private async void BtnRefresh_Click()
        {
            if (_controller.IsBusy) return;
            UpdateRunningLabel();
            await RefreshListAsync();
        }

        private void BtnBrowse_Click()
        {
            if (_controller.IsBusy) return;
            using (System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择阴阳师安装根目录（含 bin\\onmyoji.exe 的目录）";
                dialog.SelectedPath = Directory.Exists(_controller.Settings.GameRoot)
                    ? _controller.Settings.GameRoot
                    : AppSettings.DefaultGameRoot;
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string error;
                if (!_controller.TrySetGameRoot(dialog.SelectedPath, out error))
                {
                    _logger.Log(error);
                    return;
                }
                txtRoot.Text = _controller.Settings.GameRoot;
            }
        }

        private void StepOpen(int delta)
        {
            int value;
            if (!int.TryParse(txtOpenNum.Text.Trim(), out value)) value = _controller.Settings.TargetInstances;
            value = AppSettings.Clamp(value + delta, AppSettings.MinInstances, AppSettings.MaxInstances);
            txtOpenNum.Text = value.ToString();
            txtOpenNum.CaretIndex = txtOpenNum.Text.Length;
            _controller.UpdateTargetInstances(value);
        }

        private void SetArrangeCols(int columns, bool persist)
        {
            _arrangeCols = AppSettings.Clamp(columns, AppSettings.MinColumns, AppSettings.MaxColumns);
            for (int i = 0; i < 4; i++)
            {
                bool on = (i + 1) == _arrangeCols;
                if (_colSegments[i] == null) continue;
                _colSegments[i].Tag = on;
                _colSegments[i].Background = on ? Theme.B(Theme.Ink) : Brushes.Transparent;
                _colLabels[i].Foreground = Theme.B(on ? Colors.White : Theme.TextSub);
                _colLabels[i].FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
            }
            if (persist) _controller.UpdateColumns(_arrangeCols);
        }

        private void OnSoundChanged(bool enabled)
        {
            if (_suppressSoundEvents) return;
            try
            {
                _controller.SetSoundFocusEnabled(enabled);
                UpdateSoundState(enabled, false);
            }
            catch (Exception ex)
            {
                _logger.LogError("切换声音跟随失败", ex);
            }
        }

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
            _logger.Log("窗口置顶已" + (Topmost ? "开启" : "关闭") + "。");
        }

        // ==================== 列表刷新 ====================
        private void RunRefresh()
        {
            Task ignored = RefreshListAsync();
            ignored.ContinueWith(delegate(System.Threading.Tasks.Task task)
            {
                Exception ex = task.Exception == null ? null : task.Exception.GetBaseException();
                if (ex != null) _logger.LogError("刷新窗口列表失败", ex);
            }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }

        internal async Task RefreshListAsync()
        {
            if (_controller.IsBusy || _closing) return;

            try
            {
                IReadOnlyList<GameWindowInfo> windows = await _controller.GetWindowsAsync();
                if (_closing) return;

                _instancePanel.Children.Clear();
                for (int i = 0; i < windows.Count; i++)
                    _instancePanel.Children.Add(BuildInstanceRow(i + 1, windows[i]));

                bool empty = windows.Count == 0;
                _emptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
                _instanceScroll.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
                lblWinCount.Text = windows.Count + " 个窗口";
                lblScan.Text = "共 " + windows.Count + " 个 · 最后刷新 " + DateTime.Now.ToString("HH:mm:ss");
                _logger.Log("识别到阴阳师窗口 " + windows.Count + " 个。");
            }
            catch (Exception ex)
            {
                _logger.LogError("刷新窗口列表失败", ex);
            }
        }

        // ==================== 确认弹层 ====================
        private Task<bool> ConfirmAsync(string title, string message, string confirmText)
        {
            if (_confirmOverlay == null || _confirmResult != null) return Task.FromResult(false);

            _confirmTitle.Text = title;
            _confirmMessage.Text = message;
            _confirmOkButton.Content = confirmText;
            _confirmResult = new TaskCompletionSource<bool>();
            _confirmOverlay.Visibility = Visibility.Visible;
            _confirmOverlay.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
            return _confirmResult.Task;
        }

        private void FinishConfirm(bool result)
        {
            if (_confirmOverlay == null) return;
            _confirmOverlay.Visibility = Visibility.Collapsed;
            TaskCompletionSource<bool> source = _confirmResult;
            _confirmResult = null;
            if (source != null) source.TrySetResult(result);
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

        private void OpenUsageDoc()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "使用说明.txt");
            if (!File.Exists(path))
            {
                _logger.Log("未找到「使用说明.txt」。");
                return;
            }
            try { Process.Start("notepad.exe", "\"" + path + "\""); }
            catch (Exception ex) { _logger.Log("打开使用说明失败：" + ex.Message); }
        }

        private static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }
    }
}

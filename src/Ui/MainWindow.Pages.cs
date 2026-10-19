using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using OnmyojiTools.Core;

namespace OnmyojiTools.Ui
{
    /// <summary>主窗口 —— 功能页面与样式模板构建（partial）</summary>
    public partial class MainWindow
    {
        // ---------- 页面：多开管理 ----------
        private Grid BuildPageOpen()
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 卡片1：多开设置
            StackPanel spOpen = new StackPanel();
            spOpen.Children.Add(Theme.CardHeader("多开设置", "从工作目录启动 onmyoji.exe，实例间隔 8 秒"));

            Grid rowOpen = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock lbOpen = Theme.Label("目标开数：", 12.5, true, Theme.BrushFrozen(Theme.TextMain));
            Grid.SetColumn(lbOpen, 0);
            rowOpen.Children.Add(lbOpen);
            txtOpenNum = Theme.MakeTextBox("2", 72);
            Grid.SetColumn(txtOpenNum, 1);
            rowOpen.Children.Add(txtOpenNum);
            btnOpen = Theme.MakeButton("开始多开", () => { var _ = BtnOpen_Click(); }, true);
            Grid.SetColumn(btnOpen, 3);
            rowOpen.Children.Add(btnOpen);
            spOpen.Children.Add(rowOpen);

            TextBlock hintOpen = Theme.Label("目标开数范围为 1-10，自动补足当前缺口", 11);
            hintOpen.Margin = new Thickness(0, 8, 0, 0);
            spOpen.Children.Add(hintOpen);

            Grid rowRoot = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            rowRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            rowRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
            txtRoot = Theme.MakeTextBox(AppConfig.DefaultRoot, 0, true);
            txtRoot.Width = double.NaN;
            txtRoot.HorizontalAlignment = HorizontalAlignment.Stretch;
            txtRoot.TextAlignment = TextAlignment.Left;
            txtRoot.ToolTip = "阴阳师安装根目录（含 bin\\onmyoji.exe）";
            Grid.SetColumn(txtRoot, 0);
            rowRoot.Children.Add(txtRoot);
            btnBrowse = Theme.MakeButton("浏览…", () => BtnBrowse_Click());
            Grid.SetColumn(btnBrowse, 2);
            rowRoot.Children.Add(btnBrowse);
            spOpen.Children.Add(rowRoot);

            TextBlock lbRoot = Theme.Label("游戏工作目录（已保存，下次启动自动加载）", 11);
            lbRoot.Margin = new Thickness(0, 6, 0, 0);
            spOpen.Children.Add(lbRoot);

            Border cardOpen = Theme.Card(spOpen);
            Grid.SetRow(cardOpen, 0);
            page.Children.Add(cardOpen);

            // 卡片2：运行状态 + 一键关闭
            StackPanel spRun = new StackPanel();
            spRun.Children.Add(Theme.CardHeader("运行状态", "当前已启动的阴阳师实例统计"));

            Grid rowRun = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            rowRun.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowRun.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            rowRun.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border runDot = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = Theme.BrushFrozen(Theme.Success),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(runDot, 0);
            rowRun.Children.Add(runDot);
            TextBlock lbRun = Theme.Label("运行实例数量（刷新自动更新）", 12.5, true, Theme.BrushFrozen(Theme.TextMain));
            Grid.SetColumn(lbRun, 2);
            rowRun.Children.Add(lbRun);
            spRun.Children.Add(rowRun);

            btnCloseAll = Theme.MakeButton("一键关闭所有游戏窗口", () => { var _ = BtnCloseAll_Click(); }, false, true);
            btnCloseAll.Margin = new Thickness(0, 14, 0, 0);
            spRun.Children.Add(btnCloseAll);

            Border cardRun = Theme.Card(spRun);
            Grid.SetRow(cardRun, 2);
            page.Children.Add(cardRun);

            return page;
        }

        // ---------- 页面：窗口排列 ----------
        private Grid BuildPageArrange()
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel spArr = new StackPanel();
            spArr.Children.Add(Theme.CardHeader("窗口排列", "探测-定位两阶段，自动按网格排布游戏窗口"));

            Grid rowArr = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            TextBlock lbCols = Theme.Label("每行窗口数：", 12.5, true, Theme.BrushFrozen(Theme.TextMain));
            Grid.SetColumn(lbCols, 0);
            rowArr.Children.Add(lbCols);
            txtColsNum = Theme.MakeTextBox("2", 72);
            Grid.SetColumn(txtColsNum, 1);
            rowArr.Children.Add(txtColsNum);
            btnArrange = Theme.MakeButton("排列窗口", () => BtnArrange_Click(), true);
            Grid.SetColumn(btnArrange, 3);
            rowArr.Children.Add(btnArrange);
            btnRefresh = Theme.MakeButton("刷新", () => BtnRefresh_Click());
            Grid.SetColumn(btnRefresh, 5);
            rowArr.Children.Add(btnRefresh);
            spArr.Children.Add(rowArr);

            Grid listHead = new Grid { Margin = new Thickness(0, 16, 0, 6) };
            listHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            listHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock lbListTitle = Theme.Label("识别到的阴阳师窗口", 13, true, Theme.BrushFrozen(Theme.TextMain));
            Grid.SetColumn(lbListTitle, 0);
            listHead.Children.Add(lbListTitle);
            lblWinCount = Theme.Label("0 个", 11.5, true, Theme.BrushFrozen(Theme.AccentLight));
            Grid.SetColumn(lblWinCount, 1);
            listHead.Children.Add(lblWinCount);
            spArr.Children.Add(listHead);

            listWins = new ListView
            {
                Background = Theme.BrushFrozen(Theme.BgInput),
                BorderThickness = new Thickness(0),
                Foreground = Theme.BrushFrozen(Theme.TextMain),
                FontFamily = Theme.FontYH,
                FontSize = 12,
                Padding = new Thickness(2)
            };
            listWins.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            listWins.ItemContainerStyle = ListViewItemStyle();
            listWins.Height = 250;
            spArr.Children.Add(listWins);

            Border cardArr = Theme.Card(spArr);
            Grid.SetRow(cardArr, 0);
            page.Children.Add(cardArr);
            return page;
        }

        // ---------- 页面：声音控制 ----------
        private Grid BuildPageSound()
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel spSound = new StackPanel();
            spSound.Children.Add(Theme.CardHeader("声音跟随聚焦", "聚焦窗口正常音量，失焦窗口自动调小；全部失焦则全部调小"));

            Grid rowSound = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            rowSound.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowSound.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            rowSound.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            swSound = new SwitchControl(true);
            swSound.Changed += (on) => ChkSound_Changed(on);
            Grid.SetColumn(swSound, 0);
            rowSound.Children.Add(swSound);
            StackPanel spSoundText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            spSoundText.Children.Add(Theme.Label("开启声音跟随聚焦", 13, true, Theme.BrushFrozen(Theme.TextMain)));
            spSoundText.Children.Add(Theme.Label("后台每 500ms 自动检测前台窗口，仅对阴阳师进程生效", 11));
            Grid.SetColumn(spSoundText, 2);
            rowSound.Children.Add(spSoundText);
            spSound.Children.Add(rowSound);

            Border cardSound = Theme.Card(spSound);
            Grid.SetRow(cardSound, 0);
            page.Children.Add(cardSound);

            StackPanel spInfo = new StackPanel();
            spInfo.Children.Add(Theme.CardHeader("工作原理", "为什么需要这个功能"));
            TextBlock info1 = Theme.Label("多开阴阳师时，未聚焦的实例会持续播放声音，影响专注。", 11.5);
            info1.Margin = new Thickness(0, 2, 0, 0);
            spInfo.Children.Add(info1);
            TextBlock info2 = Theme.Label("本功能利用 Windows 音频会话 API（WASAPI）逐实例调节音量，不干扰游戏本体。", 11.5);
            info2.Margin = new Thickness(0, 6, 0, 0);
            spInfo.Children.Add(info2);
            Border cardInfo = Theme.Card(spInfo);
            Grid.SetRow(cardInfo, 2);
            page.Children.Add(cardInfo);

            return page;
        }

        // ---------- 页面：关于 ----------
        private Grid BuildPageAbout()
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel spAbout = new StackPanel();
            spAbout.Children.Add(Theme.CardHeader("关于本工具", "阴阳师桌面多开辅助"));

            TextBlock ver = Theme.Label("版本 v2.0（WPF 现代化界面）", 13, true, Theme.BrushFrozen(Theme.TextMain));
            ver.Margin = new Thickness(0, 4, 0, 0);
            spAbout.Children.Add(ver);
            TextBlock d1 = Theme.Label("· 多开管理：一键补足目标实例数，支持 1-10 开", 11.5);
            d1.Margin = new Thickness(0, 10, 0, 0);
            spAbout.Children.Add(d1);
            TextBlock d2 = Theme.Label("· 窗口排列：探测-定位两阶段，自动按网格排布", 11.5);
            d2.Margin = new Thickness(0, 6, 0, 0);
            spAbout.Children.Add(d2);
            TextBlock d3 = Theme.Label("· 声音跟随聚焦：WASAPI 逐实例音量控制", 11.5);
            d3.Margin = new Thickness(0, 6, 0, 0);
            spAbout.Children.Add(d3);
            Border cardAbout = Theme.Card(spAbout);
            Grid.SetRow(cardAbout, 0);
            page.Children.Add(cardAbout);

            StackPanel spTip = new StackPanel();
            spTip.Children.Add(Theme.CardHeader("使用提示", "常见问题"));
            TextBlock t1 = Theme.Label("如果操作无反应，请确认窗口未被遮挡；以管理员权限运行可稳定移动游戏窗口。", 11.5);
            t1.Margin = new Thickness(0, 2, 0, 0);
            spTip.Children.Add(t1);
            TextBlock t2 = Theme.Label("首次使用请在「多开管理」中选择正确的阴阳师安装根目录（含 bin\\onmyoji.exe）。", 11.5);
            t2.Margin = new Thickness(0, 6, 0, 0);
            spTip.Children.Add(t2);
            Border cardTip = Theme.Card(spTip);
            Grid.SetRow(cardTip, 2);
            page.Children.Add(cardTip);

            return page;
        }

        // ---------- 日志卡片 ----------
        private Border BuildLogCard()
        {
            StackPanel spLog = new StackPanel();
            Button btnClearLog = Theme.MakeButton("清空", () => txtLog.Clear(), false);
            btnClearLog.Width = 64;
            btnClearLog.HorizontalAlignment = HorizontalAlignment.Right;
            spLog.Children.Add(Theme.CardHeader("运行日志", "操作过程与结果都会记录在这里", btnClearLog));

            txtLog = new TextBox
            {
                Background = Theme.BrushFrozen(Theme.BgInput),
                Foreground = Theme.BrushFrozen(Theme.TextMain),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 8, 10, 8),
                Height = 126
            };
            txtLog.Template = LogTextBoxTemplate();
            spLog.Children.Add(txtLog);
            return Theme.Card(spLog);
        }

        private static Style ListViewItemStyle()
        {
            Style st = new Style(typeof(ListViewItem));
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(ListViewItem.BackgroundProperty));
            bd.SetValue(Border.PaddingProperty, new Thickness(10, 5, 10, 5));
            bd.SetValue(Border.MarginProperty, new Thickness(0, 1, 0, 1));
            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            bd.AppendChild(cp);
            ControlTemplate t = new ControlTemplate(typeof(ListViewItem));
            t.VisualTree = bd;
            Trigger hov = new Trigger { Property = ListViewItem.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(ListViewItem.BackgroundProperty, Theme.BrushFrozen(Theme.BgHover)));
            t.Triggers.Add(hov);
            Trigger sel = new Trigger { Property = ListViewItem.IsSelectedProperty, Value = true };
            sel.Setters.Add(new Setter(ListViewItem.BackgroundProperty, Theme.BrushFrozen(Theme.BgSel)));
            t.Triggers.Add(sel);
            st.Setters.Add(new Setter(Control.TemplateProperty, t));
            st.Setters.Add(new Setter(Control.ForegroundProperty, Theme.BrushFrozen(Theme.TextMain)));
            st.Setters.Add(new Setter(ListViewItem.BackgroundProperty, Brushes.Transparent));
            st.Setters.Add(new Setter(ListViewItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            st.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(0)));
            return st;
        }

        private static ControlTemplate LogTextBoxTemplate()
        {
            // 与 TextBoxTemplate 同理：PART_ContentHost 必须用 x:Name 注册进 NameScope，
            // FrameworkElementFactory 设名称会导致日志框内容不显示。
            const string xaml =
@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                   xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                   TargetType='TextBox'>
    <Border CornerRadius='8' Background='#0E0F14' BorderBrush='#22242E' BorderThickness='1'>
        <ScrollViewer x:Name='PART_ContentHost'/>
    </Border>
</ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(xaml);
        }
    }
}

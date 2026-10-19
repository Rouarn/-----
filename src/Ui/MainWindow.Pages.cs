using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
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
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            page.Children.Add(Theme.PageHeader("多开管理", "从游戏目录批量启动实例，自动补足当前缺口"));

            // 卡片1：多开设置
            StackPanel spOpen = new StackPanel();
            spOpen.Children.Add(Theme.CardHeader("多开设置", "以 8 秒间隔逐个启动，避免客户端抢占失败"));

            Grid rowOpen = new Grid();
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(128) });
            TextBlock lbOpen = Theme.Label("目标开数", 12.5, true, Theme.BrushFrozen(Theme.TextMain));
            Grid.SetColumn(lbOpen, 0);
            rowOpen.Children.Add(lbOpen);
            txtOpenNum = Theme.MakeTextBox("2", 72);
            Grid.SetColumn(txtOpenNum, 2);
            rowOpen.Children.Add(txtOpenNum);
            btnOpen = Theme.MakeButton("开始多开", () => { var _ = BtnOpen_Click(); }, true);
            Grid.SetColumn(btnOpen, 5);
            rowOpen.Children.Add(btnOpen);
            spOpen.Children.Add(rowOpen);

            TextBlock hintOpen = Theme.Label("目标开数范围 1–10 · 自动补足当前缺口", 11, false, Theme.BrushFrozen(Theme.TextDim));
            hintOpen.Margin = new Thickness(0, 9, 0, 0);
            spOpen.Children.Add(hintOpen);

            spOpen.Children.Add(Theme.Divider(16, 4));

            Grid rowRoot = new Grid();
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

            TextBlock lbRoot = Theme.Label("游戏工作目录（含 bin\\onmyoji.exe）· 修改后自动保存", 11, false, Theme.BrushFrozen(Theme.TextDim));
            lbRoot.Margin = new Thickness(0, 9, 0, 0);
            spOpen.Children.Add(lbRoot);

            Border cardOpen = Theme.Card(spOpen);
            Grid.SetRow(cardOpen, 2);
            page.Children.Add(cardOpen);

            // 卡片2：危险操作（一键关闭）
            StackPanel spDanger = new StackPanel();
            spDanger.Children.Add(Theme.CardHeader("危险操作", "优雅关闭所有游戏进程，超时后强制结束"));
            btnCloseAll = Theme.MakeButton("一键关闭所有游戏窗口", () => { var _ = BtnCloseAll_Click(); }, false, true);
            spDanger.Children.Add(btnCloseAll);

            Border cardDanger = new Border
            {
                Background = Theme.BrushFrozen(Color.FromArgb(0x12, 0xFB, 0x5C, 0x74)),
                BorderBrush = Theme.BrushFrozen(Color.FromArgb(0x33, 0xFB, 0x5C, 0x74)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(22, 16, 22, 16),
                Effect = Theme.CardShadow(),
                Child = spDanger
            };
            Grid.SetRow(cardDanger, 4);
            page.Children.Add(cardDanger);

            return page;
        }

        // ---------- 页面：窗口排列 ----------
        private Grid BuildPageArrange()
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            page.Children.Add(Theme.PageHeader("窗口排列", "探测-定位两阶段，按网格自动排布游戏窗口"));

            StackPanel spArr = new StackPanel();

            Grid rowArr = new Grid();
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
            TextBlock lbCols = Theme.Label("每行窗口数", 12.5, true, Theme.BrushFrozen(Theme.TextMain));
            Grid.SetColumn(lbCols, 0);
            rowArr.Children.Add(lbCols);
            txtColsNum = Theme.MakeTextBox("2", 72);
            Grid.SetColumn(txtColsNum, 2);
            rowArr.Children.Add(txtColsNum);
            btnArrange = Theme.MakeButton("排列窗口", () => BtnArrange_Click(), true);
            Grid.SetColumn(btnArrange, 5);
            rowArr.Children.Add(btnArrange);
            btnRefresh = Theme.MakeButton("刷新", () => BtnRefresh_Click());
            Grid.SetColumn(btnRefresh, 7);
            rowArr.Children.Add(btnRefresh);
            spArr.Children.Add(rowArr);

            Grid listHead = new Grid { Margin = new Thickness(0, 18, 0, 8) };
            listHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            listHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock lbListTitle = Theme.Label("识别到的游戏窗口", 13, true, Theme.BrushFrozen(Theme.TextMain));
            Grid.SetColumn(lbListTitle, 0);
            listHead.Children.Add(lbListTitle);
            lblWinCount = Theme.Label("0 个", 11.5, true, Theme.BrushFrozen(Theme.AccentLight));
            Grid.SetColumn(lblWinCount, 1);
            listHead.Children.Add(lblWinCount);
            spArr.Children.Add(listHead);

            listWins = new ListView
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Theme.BrushFrozen(Theme.TextMain),
                FontFamily = Theme.FontYH,
                FontSize = 12,
                Padding = new Thickness(2),
                Height = 232
            };
            listWins.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            listWins.ItemContainerStyle = ListViewItemStyle();
            spArr.Children.Add(listWins);

            Border cardArr = Theme.Card(spArr);
            Grid.SetRow(cardArr, 2);
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
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            page.Children.Add(Theme.PageHeader("声音控制", "跟随前台焦点自动调节各实例音量，多开不吵"));

            // 卡片1：开关
            StackPanel spSound = new StackPanel();
            Grid rowSound = new Grid();
            rowSound.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowSound.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            rowSound.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            swSound = new SwitchControl(true);
            swSound.Changed += (on) => ChkSound_Changed(on);
            Grid.SetColumn(swSound, 0);
            rowSound.Children.Add(swSound);
            StackPanel spSoundText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            spSoundText.Children.Add(Theme.Label("开启声音跟随聚焦", 13.5, true, Theme.BrushFrozen(Theme.TextMain)));
            spSoundText.Children.Add(Theme.Label("每 500ms 检测前台窗口 · 聚焦恢复正常音量，失焦自动调小", 11, false, Theme.BrushFrozen(Theme.TextDim)));
            Grid.SetColumn(spSoundText, 2);
            rowSound.Children.Add(spSoundText);
            spSound.Children.Add(rowSound);

            Border cardSound = Theme.Card(spSound);
            Grid.SetRow(cardSound, 2);
            page.Children.Add(cardSound);

            // 卡片2：工作原理
            StackPanel spInfo = new StackPanel();
            spInfo.Children.Add(Theme.CardHeader("工作原理", "为什么需要这个功能"));
            TextBlock info1 = Theme.Label("多开阴阳师时，未聚焦的实例会持续播放声音，影响专注与挂机体验。", 11.5);
            info1.Margin = new Thickness(0, 2, 0, 0);
            spInfo.Children.Add(info1);
            TextBlock info2 = Theme.Label("本功能利用 Windows 音频会话 API（WASAPI）逐实例调节音量，不干扰游戏本体。", 11.5);
            info2.Margin = new Thickness(0, 7, 0, 0);
            spInfo.Children.Add(info2);
            Border cardInfo = Theme.Card(spInfo);
            Grid.SetRow(cardInfo, 4);
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
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            page.Children.Add(Theme.PageHeader("关于", "版本信息与使用说明"));

            // 卡片1：版本 + 功能列表
            StackPanel spAbout = new StackPanel();
            Grid verRow = new Grid { Margin = new Thickness(0, 2, 0, 4) };
            verRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            verRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            verRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock appName = Theme.Label("阴阳师工具", 15, true, Theme.BrushFrozen(Theme.TextMain));
            Grid.SetColumn(appName, 0);
            verRow.Children.Add(appName);
            Border verBadge = Theme.Badge("Aurora UI · v2.0", Theme.BrushFrozen(Theme.BgSel), Theme.BrushFrozen(Theme.AccentLight));
            Grid.SetColumn(verBadge, 2);
            verRow.Children.Add(verBadge);
            spAbout.Children.Add(verRow);

            spAbout.Children.Add(Bullet("多开管理：一键补足目标实例数，支持 1-10 开"));
            spAbout.Children.Add(Bullet("窗口排列：探测-定位两阶段，自动按网格排布"));
            spAbout.Children.Add(Bullet("声音跟随聚焦：WASAPI 逐实例音量控制"));
            Border cardAbout = Theme.Card(spAbout);
            Grid.SetRow(cardAbout, 2);
            page.Children.Add(cardAbout);

            // 卡片2：使用提示
            StackPanel spTip = new StackPanel();
            spTip.Children.Add(Theme.CardHeader("使用提示", "常见问题"));
            TextBlock t1 = Theme.Label("如果操作无反应，请确认窗口未被遮挡；以管理员权限运行可稳定移动游戏窗口。", 11.5);
            t1.Margin = new Thickness(0, 2, 0, 0);
            spTip.Children.Add(t1);
            TextBlock t2 = Theme.Label("首次使用请在「多开管理」中选择正确的阴阳师安装根目录（含 bin\\onmyoji.exe）。", 11.5);
            t2.Margin = new Thickness(0, 7, 0, 0);
            spTip.Children.Add(t2);
            Border cardTip = Theme.Card(spTip);
            Grid.SetRow(cardTip, 4);
            page.Children.Add(cardTip);

            return page;
        }

        // 强调色圆点列表项
        private static StackPanel Bullet(string text)
        {
            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            Ellipse dot = new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = Theme.BrushFrozen(Theme.Accent),
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(dot);
            TextBlock t = new TextBlock
            {
                Text = text,
                FontFamily = Theme.FontYH,
                FontSize = 11.5,
                Foreground = Theme.BrushFrozen(Theme.TextSub),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(9, 0, 0, 0)
            };
            sp.Children.Add(t);
            return sp;
        }

        // ---------- 日志卡片 ----------
        private Border BuildLogCard()
        {
            Grid logGrid = new Grid();
            logGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            logGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            logGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Button btnClearLog = Theme.MakeButton("清空", () => txtLog.Clear(), false);
            btnClearLog.Width = 64;
            btnClearLog.Height = 30;
            logGrid.Children.Add(Theme.CardHeader("运行日志", "操作过程与结果都会记录在这里", btnClearLog));

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
                Padding = new Thickness(10, 8, 10, 8)
            };
            txtLog.Template = LogTextBoxTemplate();
            Grid.SetRow(txtLog, 2);
            logGrid.Children.Add(txtLog);

            return Theme.Card(logGrid);
        }

        private static Style ListViewItemStyle()
        {
            Style st = new Style(typeof(ListViewItem));
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(ListViewItem.BackgroundProperty));
            bd.SetValue(Border.PaddingProperty, new Thickness(10, 5, 10, 5));
            bd.SetValue(Border.MarginProperty, new Thickness(0, 1, 0, 1));
            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            bd.AppendChild(cp);
            ControlTemplate t = new ControlTemplate(typeof(ListViewItem));
            t.VisualTree = bd;
            Trigger hov = new Trigger { Property = ListViewItem.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(ListViewItem.BackgroundProperty, Theme.BrushFrozen(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF))));
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
    <Border CornerRadius='10' Background='#0D1018' BorderBrush='#242A3A' BorderThickness='1'>
        <ScrollViewer x:Name='PART_ContentHost'/>
    </Border>
</ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(xaml);
        }
    }
}

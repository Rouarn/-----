using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OnmyojiTools.Ui
{
    internal enum ButtonKind
    {
        Primary,
        Secondary,
        Ghost,
        Danger,
        Link,
        OnDark
    }

    // ==================== 视觉系统：和纸 · 墨 · 朱印 ====================
    internal static class Theme
    {
        // ---- 纸面与墨色 ----
        public static readonly Color PaperTop = Color.FromRgb(0xF9, 0xF4, 0xEB);
        public static readonly Color PaperBottom = Color.FromRgb(0xEF, 0xE6, 0xD6);
        public static readonly Color Surface = Color.FromRgb(0xFC, 0xFA, 0xF5);
        public static readonly Color SurfaceSoft = Color.FromRgb(0xF4, 0xED, 0xE1);
        public static readonly Color SurfaceHover = Color.FromRgb(0xF2, 0xEA, 0xDD);
        public static readonly Color InputBg = Color.FromRgb(0xFF, 0xFD, 0xF8);

        public static readonly Color Ink = Color.FromRgb(0x1D, 0x1B, 0x18);
        public static readonly Color InkSoft = Color.FromRgb(0x45, 0x41, 0x3A);
        public static readonly Color TextSub = Color.FromRgb(0x6B, 0x63, 0x57);
        public static readonly Color TextMuted = Color.FromRgb(0x8C, 0x82, 0x73);

        public static readonly Color Line = Color.FromRgb(0xE3, 0xD8, 0xC7);
        public static readonly Color LineStrong = Color.FromRgb(0xD2, 0xC5, 0xB0);

        // ---- 朱砂与金 ----
        public static readonly Color Accent = Color.FromRgb(0xC0, 0x45, 0x2C);
        public static readonly Color AccentHover = Color.FromRgb(0xA8, 0x37, 0x1F);
        public static readonly Color AccentPressed = Color.FromRgb(0x8E, 0x2C, 0x18);
        public static readonly Color AccentSoft = Color.FromRgb(0xF7, 0xE4, 0xDC);
        public static readonly Color AccentLine = Color.FromRgb(0xE8, 0xC3, 0xB6);
        public static readonly Color Gold = Color.FromRgb(0xA6, 0x7C, 0x2E);

        public static readonly Color Green = Color.FromRgb(0x4F, 0x7A, 0x58);
        public static readonly Color GreenSoft = Color.FromRgb(0xE4, 0xED, 0xE3);
        public static readonly Color Amber = Color.FromRgb(0xA5, 0x74, 0x1F);

        // ---- 终端 ----
        public static readonly Color TerminalBg = Color.FromRgb(0x19, 0x17, 0x14);
        public static readonly Color TerminalLine = Color.FromRgb(0x2C, 0x27, 0x21);
        public static readonly Color TerminalText = Color.FromRgb(0xE4, 0xDC, 0xCC);
        public static readonly Color TerminalDim = Color.FromRgb(0x93, 0x89, 0x79);

        public static readonly FontFamily FontUI = new FontFamily("Microsoft YaHei UI");
        public static readonly FontFamily FontMono = new FontFamily("Consolas");
        public static readonly FontFamily FontSerif = new FontFamily("Georgia");
        public static readonly FontFamily FontSeal = new FontFamily("KaiTi, SimSun, Microsoft YaHei UI");
        public static readonly FontFamily FontIcon = new FontFamily("Segoe MDL2 Assets");

        public static SolidColorBrush B(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static SolidColorBrush BA(byte a, Color c)
        {
            SolidColorBrush b = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
            b.Freeze();
            return b;
        }

        public static Brush PaperBackground()
        {
            LinearGradientBrush g = new LinearGradientBrush();
            g.StartPoint = new Point(0, 0);
            g.EndPoint = new Point(0, 1);
            g.GradientStops.Add(new GradientStop(PaperTop, 0));
            g.GradientStops.Add(new GradientStop(PaperBottom, 1));
            g.Freeze();
            return g;
        }

        // ==================== 文本 ====================
        public static TextBlock Text(string text, double size, FontWeight weight, Color color)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = FontUI,
                FontSize = size,
                FontWeight = weight,
                Foreground = B(color),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static TextBlock Label(string text, double size = 11.5, Color? color = null)
        {
            return Text(text, size, FontWeights.Normal, color ?? TextSub);
        }

        public static TextBlock Meta(string text)
        {
            return Text(text, 10.5, FontWeights.Normal, TextMuted);
        }

        // ==================== 容器 ====================
        public static Border Card(UIElement child)
        {
            return Card(child, new Thickness(18, 16, 18, 16));
        }

        public static Border Card(UIElement child, Thickness padding)
        {
            return new Border
            {
                Background = B(Surface),
                BorderBrush = B(Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = padding,
                Child = child,
                SnapsToDevicePixels = true
            };
        }

        public static Grid CardHeader(string index, string title, string desc, FrameworkElement extra)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, string.IsNullOrEmpty(desc) ? 4 : 13) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel left = new StackPanel();
            StackPanel head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(new TextBlock
            {
                Text = index,
                FontFamily = FontSerif,
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = B(Gold),
                VerticalAlignment = VerticalAlignment.Center
            });
            head.Children.Add(new Border
            {
                Width = 1,
                Height = 11,
                Background = B(LineStrong),
                Margin = new Thickness(9, 0, 9, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            head.Children.Add(new TextBlock
            {
                Text = title,
                FontFamily = FontUI,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = B(Ink),
                VerticalAlignment = VerticalAlignment.Center
            });
            left.Children.Add(head);

            if (!string.IsNullOrEmpty(desc))
            {
                left.Children.Add(new TextBlock
                {
                    Text = desc,
                    FontFamily = FontUI,
                    FontSize = 11,
                    Foreground = B(TextSub),
                    Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
            }
            Grid.SetColumn(left, 0);
            g.Children.Add(left);

            if (extra != null)
            {
                extra.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(extra, 1);
                g.Children.Add(extra);
            }
            return g;
        }

        public static Border Divider(double top = 14, double bottom = 12)
        {
            return new Border
            {
                Height = 1,
                Background = B(Line),
                Margin = new Thickness(0, top, 0, bottom),
                SnapsToDevicePixels = true
            };
        }

        public static Border Hairline()
        {
            return new Border { Height = 1, Background = B(Line), SnapsToDevicePixels = true };
        }

        // ==================== 按钮 ====================
        public static Button MakeButton(string text, Action onClick, ButtonKind kind)
        {
            return MakeButton(text, onClick, kind, 40);
        }

        public static Button MakeButton(string text, Action onClick, ButtonKind kind, double height)
        {
            Button b = new Button
            {
                Content = text,
                FontFamily = FontUI,
                FontSize = kind == ButtonKind.Link ? 11.2 : 12.2,
                FontWeight = FontWeights.SemiBold,
                Height = height,
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                SnapsToDevicePixels = true
            };

            Brush hoverBg, hoverBorder, hoverFg, pressBg, pressBorder, pressFg;

            switch (kind)
            {
                case ButtonKind.Primary:
                    b.Background = B(Accent);
                    b.Foreground = B(Colors.White);
                    b.BorderBrush = B(Accent);
                    hoverBg = B(AccentHover); hoverBorder = B(AccentHover); hoverFg = B(Colors.White);
                    pressBg = B(AccentPressed); pressBorder = B(AccentPressed); pressFg = B(Colors.White);
                    break;
                case ButtonKind.Secondary:
                    b.Background = B(Surface);
                    b.Foreground = B(Ink);
                    b.BorderBrush = B(LineStrong);
                    hoverBg = B(SurfaceHover); hoverBorder = B(Color.FromRgb(0xC3, 0xB4, 0x9C)); hoverFg = B(Ink);
                    pressBg = B(Color.FromRgb(0xEB, 0xE2, 0xD3)); pressBorder = B(Color.FromRgb(0xB5, 0xA4, 0x89)); pressFg = B(Ink);
                    break;
                case ButtonKind.Danger:
                    b.Background = B(Color.FromRgb(0xFB, 0xF1, 0xEC));
                    b.Foreground = B(Accent);
                    b.BorderBrush = B(AccentLine);
                    hoverBg = B(Color.FromRgb(0xF6, 0xE1, 0xD9)); hoverBorder = B(Color.FromRgb(0xD9, 0x9F, 0x90)); hoverFg = B(AccentHover);
                    pressBg = B(Color.FromRgb(0xEF, 0xD2, 0xC7)); pressBorder = B(AccentHover); pressFg = B(AccentPressed);
                    break;
                case ButtonKind.OnDark:
                    b.Background = Brushes.Transparent;
                    b.Foreground = B(TerminalDim);
                    b.BorderBrush = Brushes.Transparent;
                    hoverBg = B(Color.FromRgb(0x2A, 0x25, 0x1F)); hoverBorder = B(Color.FromRgb(0x2A, 0x25, 0x1F)); hoverFg = B(TerminalText);
                    pressBg = B(Color.FromRgb(0x33, 0x2D, 0x25)); pressBorder = B(Color.FromRgb(0x33, 0x2D, 0x25)); pressFg = B(TerminalText);
                    break;
                case ButtonKind.Link:
                    b.Background = Brushes.Transparent;
                    b.Foreground = B(Accent);
                    b.BorderBrush = Brushes.Transparent;
                    b.Padding = new Thickness(0);
                    hoverBg = Brushes.Transparent; hoverBorder = Brushes.Transparent; hoverFg = B(AccentHover);
                    pressBg = Brushes.Transparent; pressBorder = Brushes.Transparent; pressFg = B(AccentPressed);
                    break;
                default:
                    b.Background = Brushes.Transparent;
                    b.Foreground = B(TextSub);
                    b.BorderBrush = Brushes.Transparent;
                    hoverBg = B(SurfaceHover); hoverBorder = B(SurfaceHover); hoverFg = B(Ink);
                    pressBg = B(Color.FromRgb(0xE9, 0xDF, 0xCF)); pressBorder = B(Color.FromRgb(0xE9, 0xDF, 0xCF)); pressFg = B(Ink);
                    break;
            }

            b.BorderThickness = new Thickness(1);
            b.Template = ButtonTemplate(hoverBg, hoverBorder, hoverFg, pressBg, pressBorder, pressFg);
            if (onClick != null) b.Click += delegate { onClick(); };
            return b;
        }

        private static ControlTemplate ButtonTemplate(Brush hoverBg, Brush hoverBorder, Brush hoverFg,
            Brush pressBg, Brush pressBorder, Brush pressFg)
        {
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.Name = "Bd";
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bd.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            bd.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            bd.SetValue(FrameworkElement.SnapsToDevicePixelsProperty, true);

            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetValue(FrameworkElement.MarginProperty, new Thickness(14, 0, 14, 0));
            bd.AppendChild(cp);

            ControlTemplate t = new ControlTemplate(typeof(Button));
            t.VisualTree = bd;

            Trigger hov = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(Button.BackgroundProperty, hoverBg));
            hov.Setters.Add(new Setter(Button.BorderBrushProperty, hoverBorder));
            hov.Setters.Add(new Setter(Button.ForegroundProperty, hoverFg));
            t.Triggers.Add(hov);

            Trigger prs = new Trigger { Property = Button.IsPressedProperty, Value = true };
            prs.Setters.Add(new Setter(Button.BackgroundProperty, pressBg));
            prs.Setters.Add(new Setter(Button.BorderBrushProperty, pressBorder));
            prs.Setters.Add(new Setter(Button.ForegroundProperty, pressFg));
            t.Triggers.Add(prs);

            Trigger dis = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            dis.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
            t.Triggers.Add(dis);

            return t;
        }

        // 标题栏窗口按钮：最小化 / 关闭 / 置顶
        public static Button MakeChromeButton(string glyph, bool close, Action onClick, string tip)
        {
            Button b = new Button
            {
                Content = glyph,
                FontFamily = FontIcon,
                FontSize = 11,
                Width = 34,
                Height = 30,
                Foreground = B(TextSub),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null,
                ToolTip = string.IsNullOrEmpty(tip) ? null : tip,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true
            };

            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
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
            hov.Setters.Add(new Setter(Button.BackgroundProperty, B(close ? Accent : SurfaceHover)));
            hov.Setters.Add(new Setter(Button.BorderBrushProperty, B(close ? Accent : SurfaceHover)));
            hov.Setters.Add(new Setter(Button.ForegroundProperty, B(close ? Colors.White : Ink)));
            t.Triggers.Add(hov);
            Trigger prs = new Trigger { Property = Button.IsPressedProperty, Value = true };
            prs.Setters.Add(new Setter(Button.BackgroundProperty, B(close ? AccentPressed : Color.FromRgb(0xE7, 0xDD, 0xCD))));
            prs.Setters.Add(new Setter(Button.BorderBrushProperty, B(close ? AccentPressed : Color.FromRgb(0xE7, 0xDD, 0xCD))));
            prs.Setters.Add(new Setter(Button.ForegroundProperty, B(close ? Colors.White : Ink)));
            t.Triggers.Add(prs);
            b.Template = t;

            if (onClick != null) b.Click += delegate { onClick(); };
            return b;
        }

        // 小图标按钮（如刷新）：浅色底、悬停变深
        public static Button MakeIconButton(string glyph, string tip, Action onClick)
        {
            Button b = new Button
            {
                Content = glyph,
                FontFamily = FontIcon,
                FontSize = 11,
                Width = 30,
                Height = 28,
                Foreground = B(TextSub),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = B(Line),
                Cursor = Cursors.Hand,
                Focusable = false,
                FocusVisualStyle = null,
                ToolTip = tip,
                SnapsToDevicePixels = true
            };

            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
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
            hov.Setters.Add(new Setter(Button.BackgroundProperty, B(SurfaceHover)));
            hov.Setters.Add(new Setter(Button.ForegroundProperty, B(Ink)));
            t.Triggers.Add(hov);
            b.Template = t;

            if (onClick != null) b.Click += delegate { onClick(); };
            return b;
        }

        // ==================== 输入框 ====================
        private static ControlTemplate _textBoxTemplate;

        public static TextBox MakeTextBox(string def, double width, bool readOnly)
        {
            TextBox t = new TextBox
            {
                Text = def,
                FontFamily = FontUI,
                FontSize = 11.8,
                Width = width <= 0 ? double.NaN : width,
                Height = 38,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Left,
                Background = B(readOnly ? SurfaceSoft : InputBg),
                Foreground = B(readOnly ? TextSub : Ink),
                BorderBrush = B(Line),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 0, 10, 0),
                IsReadOnly = readOnly,
                CaretBrush = B(Accent),
                SelectionBrush = B(AccentSoft),
                FocusVisualStyle = null,
                SnapsToDevicePixels = true,
                Template = TextBoxTemplate()
            };
            SpellCheck.SetIsEnabled(t, false);
            return t;
        }

        private static ControlTemplate TextBoxTemplate()
        {
            if (_textBoxTemplate != null) return _textBoxTemplate;
            // PART_ContentHost 必须经 XAML 命名作用域注册；用 FrameworkElementFactory 设名字会取不到，文本框内容不显示。
            const string xaml =
@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                   xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                   TargetType='TextBox'>
    <Border x:Name='Bd' CornerRadius='3'
            Background='{TemplateBinding Background}'
            BorderBrush='{TemplateBinding BorderBrush}'
            BorderThickness='{TemplateBinding BorderThickness}'
            Padding='{TemplateBinding Padding}'>
        <ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center'
                      HorizontalScrollBarVisibility='Hidden' VerticalScrollBarVisibility='Hidden'/>
    </Border>
    <ControlTemplate.Triggers>
        <Trigger Property='IsMouseOver' Value='True'>
            <Setter TargetName='Bd' Property='BorderBrush' Value='#C9BAA4'/>
        </Trigger>
        <Trigger Property='IsKeyboardFocusWithin' Value='True'>
            <Setter TargetName='Bd' Property='BorderBrush' Value='#C0452C'/>
            <Setter TargetName='Bd' Property='Background' Value='#FFFDF8'/>
        </Trigger>
        <Trigger Property='IsEnabled' Value='False'>
            <Setter TargetName='Bd' Property='Opacity' Value='0.55'/>
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>";
            _textBoxTemplate = (ControlTemplate)XamlReader.Parse(xaml);
            return _textBoxTemplate;
        }

        private static ControlTemplate _logBoxTemplate;

        public static TextBox MakeLogBox()
        {
            TextBox t = new TextBox
            {
                FontFamily = FontMono,
                FontSize = 10.7,
                Foreground = B(TerminalText),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 2, 0, 2),
                IsReadOnly = true,
                IsReadOnlyCaretVisible = false,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                SelectionBrush = B(Color.FromRgb(0x4A, 0x41, 0x33)),
                SelectionOpacity = 1,
                FocusVisualStyle = null,
                Template = LogBoxTemplate()
            };
            return t;
        }

        private static ControlTemplate LogBoxTemplate()
        {
            if (_logBoxTemplate != null) return _logBoxTemplate;
            const string xaml =
@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                   xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                   TargetType='TextBox'>
    <ScrollViewer x:Name='PART_ContentHost' Focusable='False'
                  HorizontalScrollBarVisibility='Disabled'
                  VerticalScrollBarVisibility='Auto'/>
</ControlTemplate>";
            _logBoxTemplate = (ControlTemplate)XamlReader.Parse(xaml);
            return _logBoxTemplate;
        }

        // ==================== 小部件 ====================
        public static Border Badge(string text, Color fg, Color bg)
        {
            return new Border
            {
                Background = B(bg),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(7, 2, 7, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = text,
                    FontFamily = FontUI,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = B(fg)
                }
            };
        }

        public static Ellipse Dot(double size, Color color)
        {
            return new Ellipse
            {
                Width = size,
                Height = size,
                Fill = B(color),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // 朱印：带内框的印章方块
        public static Border Seal(double size)
        {
            Border seal = new Border
            {
                Width = size,
                Height = size,
                Background = B(Accent),
                CornerRadius = new CornerRadius(4),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(-3),
                SnapsToDevicePixels = true
            };

            Grid g = new Grid();
            g.Children.Add(new Border
            {
                Margin = new Thickness(size * 0.09),
                BorderBrush = BA(0x5A, Colors.White),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2)
            });
            g.Children.Add(new TextBlock
            {
                Text = "阴",
                FontFamily = FontSeal,
                FontSize = size * 0.56,
                FontWeight = FontWeights.Bold,
                Foreground = B(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0)
            });
            seal.Child = g;
            return seal;
        }

        // 细线装饰圆（背景水印）
        public static Ellipse Ring(double size, Color color, double thickness)
        {
            Ellipse e = new Ellipse
            {
                Width = size,
                Height = size,
                Stroke = B(color),
                StrokeThickness = thickness,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            };
            return e;
        }

        // ==================== 全局资源：细滚动条 + 深色提示 ====================
        private static ResourceDictionary _commonResources;

        public static ResourceDictionary CommonResources()
        {
            if (_commonResources != null) return _commonResources;
            const string xaml =
@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <Style TargetType='{x:Type ToolTip}'>
        <Setter Property='OverridesDefaultStyle' Value='True'/>
        <Setter Property='HasDropShadow' Value='False'/>
        <Setter Property='Background' Value='#1D1B18'/>
        <Setter Property='Foreground' Value='#F6F1E7'/>
        <Setter Property='FontFamily' Value='Microsoft YaHei UI'/>
        <Setter Property='FontSize' Value='11'/>
        <Setter Property='Padding' Value='9,6'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='{x:Type ToolTip}'>
                    <Border Background='{TemplateBinding Background}' CornerRadius='3'
                            Padding='{TemplateBinding Padding}'>
                        <ContentPresenter/>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType='{x:Type ScrollBar}'>
        <Setter Property='Width' Value='7'/>
        <Setter Property='MinWidth' Value='7'/>
        <Setter Property='Background' Value='Transparent'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='{x:Type ScrollBar}'>
                    <Grid Background='Transparent'>
                        <Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='Vertical'
                               Minimum='{TemplateBinding Minimum}'
                               Maximum='{TemplateBinding Maximum}'
                               ViewportSize='{TemplateBinding ViewportSize}'
                               Value='{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
                            <Track.DecreaseRepeatButton>
                                <RepeatButton Command='ScrollBar.PageUpCommand' Focusable='False'
                                              Opacity='0' Background='Transparent'/>
                            </Track.DecreaseRepeatButton>
                            <Track.Thumb>
                                <Thumb MinHeight='24'>
                                    <Thumb.Template>
                                        <ControlTemplate TargetType='{x:Type Thumb}'>
                                            <Border x:Name='ThumbBd' CornerRadius='3'
                                                    Background='#C6B69E' Margin='1,2'/>
                                            <ControlTemplate.Triggers>
                                                <Trigger Property='IsMouseOver' Value='True'>
                                                    <Setter TargetName='ThumbBd' Property='Background' Value='#A9977C'/>
                                                </Trigger>
                                                <Trigger Property='IsDragging' Value='True'>
                                                    <Setter TargetName='ThumbBd' Property='Background' Value='#86745A'/>
                                                </Trigger>
                                            </ControlTemplate.Triggers>
                                        </ControlTemplate>
                                    </Thumb.Template>
                                </Thumb>
                            </Track.Thumb>
                            <Track.IncreaseRepeatButton>
                                <RepeatButton Command='ScrollBar.PageDownCommand' Focusable='False'
                                              Opacity='0' Background='Transparent'/>
                            </Track.IncreaseRepeatButton>
                        </Track>
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>";
            _commonResources = (ResourceDictionary)XamlReader.Parse(xaml);
            return _commonResources;
        }
    }
}

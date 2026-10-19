using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Markup;

namespace OnmyojiTools.Ui
{
    // ==================== 主题与控件工厂（Aurora 极光玻璃拟态 · 深色） ====================
    internal static class Theme
    {
        // ---- 色板：墨黑基底 + 极光紫蓝强调 + 玻璃半透明表面 ----
        public static readonly Color BgMain = Color.FromRgb(0x0A, 0x0C, 0x12);          // 基底近黑
        public static readonly Color BgTop = Color.FromRgb(0x16, 0x1B, 0x2E);           // 壳层顶部微光
        public static readonly Color BgInput = Color.FromRgb(0x0D, 0x10, 0x18);         // 输入框底色
        public static readonly Color BgHover = Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);  // 玻璃悬停
        public static readonly Color BgSel = Color.FromArgb(0x28, 0x8B, 0x7C, 0xF8);    // 强调选中底
        public static readonly Color BorderLine = Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF); // 玻璃描边
        public static readonly Color BorderHover = Color.FromArgb(0x3C, 0xFF, 0xFF, 0xFF);
        public static readonly Color TextMain = Color.FromRgb(0xF3, 0xF5, 0xF9);
        public static readonly Color TextSub = Color.FromRgb(0x9A, 0xA3, 0xB5);
        public static readonly Color TextDim = Color.FromRgb(0x67, 0x6F, 0x82);
        public static readonly Color Accent = Color.FromRgb(0x8B, 0x7C, 0xF8);          // 极光紫
        public static readonly Color AccentLight = Color.FromRgb(0xA8, 0x9C, 0xFA);
        public static readonly Color AccentDark = Color.FromRgb(0x5B, 0x7B, 0xF0);      // 过渡蓝
        public static readonly Color AccentBright = Color.FromRgb(0xC6, 0xBB, 0xFD);
        public static readonly Color Danger = Color.FromRgb(0xFB, 0x5C, 0x74);
        public static readonly Color DangerDark = Color.FromRgb(0xD8, 0x27, 0x48);
        public static readonly Color DangerBright = Color.FromRgb(0xFF, 0x85, 0x9A);
        public static readonly Color Success = Color.FromRgb(0x3D, 0xDC, 0x97);
        public static readonly Color TrackOff = Color.FromRgb(0x39, 0x3F, 0x52);

        public static readonly FontFamily FontUI = new FontFamily("Segoe UI");
        public static readonly FontFamily FontYH = new FontFamily("Microsoft YaHei UI");

        public static Brush Brush(Color c) { return new SolidColorBrush(c); }
        public static Brush BrushFrozen(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        // ---- 主色渐变（紫 → 蓝） ----
        public static Brush AccentGradient()
        {
            LinearGradientBrush g = new LinearGradientBrush(AccentLight, AccentDark, 90d);
            g.Freeze();
            return g;
        }
        public static Brush AccentGradientHover()
        {
            LinearGradientBrush g = new LinearGradientBrush(Color.FromRgb(0xB7, 0xAD, 0xFB), Color.FromRgb(0x6F, 0x8B, 0xF5), 90d);
            g.Freeze();
            return g;
        }
        public static Brush AccentGradientPressed()
        {
            LinearGradientBrush g = new LinearGradientBrush(Color.FromRgb(0x7A, 0x6B, 0xF0), Color.FromRgb(0x4A, 0x5F, 0xD2), 90d);
            g.Freeze();
            return g;
        }

        // ---- 危险色渐变 ----
        public static Brush DangerGradient()
        {
            LinearGradientBrush g = new LinearGradientBrush(DangerBright, DangerDark, 90d);
            g.Freeze();
            return g;
        }
        public static Brush DangerGradientHover()
        {
            LinearGradientBrush g = new LinearGradientBrush(Color.FromRgb(0xFF, 0xA0, 0xB0), Color.FromRgb(0xE7, 0x36, 0x58), 90d);
            g.Freeze();
            return g;
        }
        public static Brush DangerGradientPressed()
        {
            LinearGradientBrush g = new LinearGradientBrush(Color.FromRgb(0xC2, 0x1F, 0x40), Color.FromRgb(0x9E, 0x18, 0x33), 90d);
            g.Freeze();
            return g;
        }

        // ---- 玻璃卡片填充（顶部略亮的半透明白） ----
        public static Brush CardFill()
        {
            LinearGradientBrush g = new LinearGradientBrush();
            g.StartPoint = new Point(0, 0);
            g.EndPoint = new Point(0, 1);
            g.GradientStops.Add(new GradientStop(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF), 0));
            g.GradientStops.Add(new GradientStop(Color.FromArgb(0x0C, 0xFF, 0xFF, 0xFF), 1));
            g.Freeze();
            return g;
        }

        // ---- 壳层背景（顶部深蓝黑 → 底部近黑） ----
        public static Brush ShellBackground()
        {
            LinearGradientBrush g = new LinearGradientBrush();
            g.StartPoint = new Point(0, 0);
            g.EndPoint = new Point(0, 1);
            g.GradientStops.Add(new GradientStop(BgTop, 0));
            g.GradientStops.Add(new GradientStop(Color.FromRgb(0x0D, 0x10, 0x1B), 0.55));
            g.GradientStops.Add(new GradientStop(BgMain, 1));
            g.Freeze();
            return g;
        }

        public static DropShadowEffect CardShadow()
        {
            return new DropShadowEffect
            {
                BlurRadius = 30,
                ShadowDepth = 12,
                Direction = 270,
                Opacity = 0.40,
                Color = Colors.Black
            };
        }

        // ---------- 通用文本 ----------
        public static TextBlock Label(string text, double size = 12.5, bool bold = false, Brush fg = null)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = FontYH,
                FontSize = size,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = fg ?? BrushFrozen(TextSub),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // ---------- 页面大标题 ----------
        public static StackPanel PageHeader(string title, string desc)
        {
            StackPanel sp = new StackPanel { Margin = new Thickness(2, 0, 0, 18) };
            sp.Children.Add(new TextBlock
            {
                Text = title,
                FontFamily = FontYH,
                FontSize = 21,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFrozen(TextMain)
            });
            sp.Children.Add(new TextBlock
            {
                Text = desc,
                FontFamily = FontYH,
                FontSize = 12,
                Foreground = BrushFrozen(TextSub),
                Margin = new Thickness(1, 5, 0, 0)
            });
            return sp;
        }

        // ---------- 细分隔线 ----------
        public static Border Divider(double topMargin = 14, double bottomMargin = 2)
        {
            return new Border
            {
                Height = 1,
                Background = BrushFrozen(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(0, topMargin, 0, bottomMargin)
            };
        }

        // ---------- 玻璃卡片容器 ----------
        public static Border Card(UIElement child)
        {
            return new Border
            {
                Background = CardFill(),
                BorderBrush = BrushFrozen(BorderLine),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(22, 18, 22, 18),
                Effect = CardShadow(),
                Child = child
            };
        }

        public static Grid CardHeader(string title, string subtitle, UIElement extra = null)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel sp = new StackPanel();
            TextBlock t = new TextBlock
            {
                Text = title,
                FontFamily = FontYH,
                FontSize = 14.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFrozen(TextMain)
            };
            sp.Children.Add(t);
            if (!string.IsNullOrEmpty(subtitle))
            {
                sp.Children.Add(new TextBlock
                {
                    Text = subtitle,
                    FontFamily = FontYH,
                    FontSize = 11.5,
                    Foreground = BrushFrozen(TextSub),
                    Margin = new Thickness(0, 3, 0, 0)
                });
            }
            Grid.SetColumn(sp, 0);
            g.Children.Add(sp);
            if (extra != null)
            {
                Grid.SetColumn(extra, 1);
                g.Children.Add(extra);
            }
            return g;
        }

        // ---------- 按钮 ----------
        public static Button MakeButton(string text, Action onClick, bool primary = false, bool danger = false)
        {
            Button b = new Button
            {
                Content = text,
                FontFamily = FontYH,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Height = 38,
                Padding = new Thickness(16, 0, 16, 0),
                Cursor = Cursors.Hand,
                Focusable = false,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            b.BorderThickness = new Thickness(0);
            b.BorderBrush = Brushes.Transparent;
            if (primary)
            {
                b.Background = AccentGradient();
                b.Foreground = BrushFrozen(Colors.White);
                b.Template = ButtonTemplate(AccentGradientHover(), AccentGradientPressed());
            }
            else if (danger)
            {
                b.Background = DangerGradient();
                b.Foreground = BrushFrozen(Colors.White);
                b.Template = ButtonTemplate(DangerGradientHover(), DangerGradientPressed());
            }
            else
            {
                // 调和按钮：半透明白填充，无描边
                b.Background = BrushFrozen(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
                b.Foreground = BrushFrozen(TextMain);
                b.Template = ButtonTemplate(BrushFrozen(BgHover), BrushFrozen(Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF)));
            }

            if (onClick != null) b.Click += (s, e) => onClick();
            return b;
        }

        private static ControlTemplate ButtonTemplate(Brush hoverBg, Brush pressedBg)
        {
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bd.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            bd.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));

            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetValue(ContentPresenter.MarginProperty, new Thickness(12, 0, 12, 0));
            bd.AppendChild(cp);

            ControlTemplate t = new ControlTemplate(typeof(Button));
            t.VisualTree = bd;

            Trigger hov = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(Button.BackgroundProperty, hoverBg));
            t.Triggers.Add(hov);

            Trigger prs = new Trigger { Property = Button.IsPressedProperty, Value = true };
            prs.Setters.Add(new Setter(Button.BackgroundProperty, pressedBg));
            t.Triggers.Add(prs);

            Trigger dis = new Trigger { Property = Button.IsEnabledProperty, Value = false };
            dis.Setters.Add(new Setter(Button.BackgroundProperty, BrushFrozen(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF))));
            dis.Setters.Add(new Setter(Button.ForegroundProperty, BrushFrozen(Color.FromRgb(0x5A, 0x61, 0x6E))));
            t.Triggers.Add(dis);

            return t;
        }

        // ---------- 输入框 ----------
        public static TextBox MakeTextBox(string def, int width = 62, bool readOnly = false)
        {
            TextBox t = new TextBox
            {
                Text = def,
                FontFamily = FontYH,
                FontSize = 12.5,
                Width = width,
                Height = 38,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Background = BrushFrozen(BgInput),
                Foreground = BrushFrozen(TextMain),
                BorderBrush = BrushFrozen(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 0, 10, 0),
                IsReadOnly = readOnly,
                CaretBrush = BrushFrozen(AccentBright)
            };
            t.Template = TextBoxTemplate();
            return t;
        }

        private static ControlTemplate TextBoxTemplate()
        {
            // 注意：不能用 FrameworkElementFactory 设置 PART_ContentHost 名称——
            // 纯代码工厂的名称不会注册进模板 NameScope，TextBox 内部 GetTemplateChild("PART_ContentHost")
            // 会返回 null，导致文本框渲染出边框背景但文本/光标完全不显示。
            // 因此用 XamlReader 解析 XAML 构建模板，x:Name 才能正确注册。
            const string xaml =
@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                   xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                   TargetType='TextBox'>
    <Border CornerRadius='10'
            Background='{TemplateBinding Background}'
            BorderBrush='{TemplateBinding BorderBrush}'
            BorderThickness='{TemplateBinding BorderThickness}'
            Padding='10,0,10,0'>
        <ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center'/>
    </Border>
    <ControlTemplate.Triggers>
        <Trigger Property='IsKeyboardFocusWithin' Value='True'>
            <Setter Property='BorderBrush' Value='#8B7CF8'/>
            <Setter Property='BorderThickness' Value='1.5'/>
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(xaml);
        }

        // ---------- 徽标（胶囊） ----------
        public static Border Badge(string text, Brush bg, Brush fg)
        {
            Border b = new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(10, 3, 10, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = text,
                    FontFamily = FontYH,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = fg
                }
            };
            return b;
        }
    }
}

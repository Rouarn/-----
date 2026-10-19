using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Markup;

namespace OnmyojiTools.Ui
{
    // ==================== 主题与控件工厂（现代化深色 Material 风格） ====================
    internal static class Theme
    {
        // 色板（赛博霓虹深色：近黑基底 + 锌色阶卡片 + 电光青蓝强调）
        public static readonly Color BgMain = Color.FromRgb(0x0A, 0x0B, 0x0E);
        public static readonly Color BgCard = Color.FromRgb(0x15, 0x16, 0x1C);
        public static readonly Color BgInput = Color.FromRgb(0x0E, 0x0F, 0x14);
        public static readonly Color BgHover = Color.FromRgb(0x1E, 0x20, 0x28);
        public static readonly Color BgSel = Color.FromRgb(0x16, 0x21, 0x2E);
        public static readonly Color BorderLine = Color.FromRgb(0x22, 0x24, 0x2E);
        public static readonly Color BorderHover = Color.FromRgb(0x3E, 0x42, 0x50);
        public static readonly Color TextMain = Color.FromRgb(0xF5, 0xF6, 0xFA);
        public static readonly Color TextSub = Color.FromRgb(0x9A, 0xA0, 0xB0);
        public static readonly Color Accent = Color.FromRgb(0x22, 0xD3, 0xEE);
        public static readonly Color AccentLight = Color.FromRgb(0x67, 0xE8, 0xF9);
        public static readonly Color AccentDark = Color.FromRgb(0x08, 0x91, 0xB2);
        public static readonly Color AccentBright = Color.FromRgb(0xA5, 0xF3, 0xFC);
        public static readonly Color Danger = Color.FromRgb(0xF4, 0x3F, 0x5E);
        public static readonly Color DangerDark = Color.FromRgb(0xBE, 0x12, 0x3C);
        public static readonly Color DangerBright = Color.FromRgb(0xFB, 0x71, 0x85);
        public static readonly Color Success = Color.FromRgb(0x34, 0xD3, 0x99);
        public static readonly Color TrackOff = Color.FromRgb(0x33, 0x37, 0x40);
        public static readonly Color NavHover = Color.FromRgb(0x18, 0x1B, 0x23);
        public static readonly Color NavSel = Color.FromRgb(0x0F, 0x25, 0x30);

        public static readonly FontFamily FontUI = new FontFamily("Segoe UI");
        public static readonly FontFamily FontYH = new FontFamily("Microsoft YaHei UI");

        public static Brush Brush(Color c) { return new SolidColorBrush(c); }
        public static Brush BrushFrozen(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        // 垂直渐变画刷（霓虹主色系）
        public static Brush AccentGradient()
        {
            LinearGradientBrush g = new LinearGradientBrush(AccentLight, AccentDark, 90d);
            g.Freeze();
            return g;
        }
        public static Brush AccentGradientHover()
        {
            LinearGradientBrush g = new LinearGradientBrush(AccentBright, Color.FromRgb(0x0E, 0x7F, 0x9E), 90d);
            g.Freeze();
            return g;
        }
        public static Brush AccentGradientPressed()
        {
            LinearGradientBrush g = new LinearGradientBrush(Color.FromRgb(0x06, 0x6E, 0x88), Color.FromRgb(0x05, 0x5C, 0x74), 90d);
            g.Freeze();
            return g;
        }
        public static Brush DangerGradient()
        {
            LinearGradientBrush g = new LinearGradientBrush(DangerBright, DangerDark, 90d);
            g.Freeze();
            return g;
        }
        public static Brush DangerGradientHover()
        {
            LinearGradientBrush g = new LinearGradientBrush(Color.FromRgb(0xFB, 0x8F, 0xA0), Color.FromRgb(0xCF, 0x2E, 0x4C), 90d);
            g.Freeze();
            return g;
        }
        public static Brush DangerGradientPressed()
        {
            LinearGradientBrush g = new LinearGradientBrush(Color.FromRgb(0x99, 0x0E, 0x30), Color.FromRgb(0x86, 0x0C, 0x2A), 90d);
            g.Freeze();
            return g;
        }

        public static DropShadowEffect CardShadow()
        {
            return new DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 6,
                Opacity = 0.42,
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

        // ---------- 卡片容器 ----------
        public static Border Card(UIElement child)
        {
            Border b = new Border
            {
                Background = BrushFrozen(BgCard),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18, 16, 18, 16),
                Effect = CardShadow(),
                Child = child
            };
            return b;
        }

        public static Grid CardHeader(string title, string subtitle, UIElement extra = null)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel sp = new StackPanel();
            TextBlock t = new TextBlock
            {
                Text = title,
                FontFamily = FontYH,
                FontSize = 15,
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
                    FontSize = 11,
                    Foreground = BrushFrozen(TextSub),
                    Margin = new Thickness(0, 2, 0, 0)
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
                b.Background = BrushFrozen(Color.FromArgb(0, 0, 0, 0)); // 透明
                b.Foreground = BrushFrozen(TextMain);
                b.BorderThickness = new Thickness(1);
                b.BorderBrush = BrushFrozen(BorderLine);
                b.Template = ButtonTemplate(BrushFrozen(BgHover), BrushFrozen(BgSel));
            }

            if (onClick != null) b.Click += (s, e) => onClick();
            return b;
        }

        private static ControlTemplate ButtonTemplate(Brush hoverBg, Brush pressedBg)
        {
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
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
            dis.Setters.Add(new Setter(Button.BackgroundProperty, BrushFrozen(Color.FromRgb(0x1C, 0x21, 0x2A))));
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
                BorderBrush = BrushFrozen(BorderLine),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 0, 10, 0),
                IsReadOnly = readOnly,
                CaretBrush = BrushFrozen(AccentLight)
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
    <Border CornerRadius='12'
            Background='{TemplateBinding Background}'
            BorderBrush='{TemplateBinding BorderBrush}'
            BorderThickness='{TemplateBinding BorderThickness}'
            Padding='10,0,10,0'>
        <ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center'/>
    </Border>
    <ControlTemplate.Triggers>
        <Trigger Property='IsKeyboardFocusWithin' Value='True'>
            <Setter Property='BorderBrush' Value='#22D3EE'/>
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
                CornerRadius = new CornerRadius(12),
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

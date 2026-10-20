using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace OnmyojiTools.Ui
{
    /// <summary>朱印开关：44×24 的扁平滑轨，开启时填充朱砂色</summary>
    public class SwitchControl : Border
    {
        private readonly Border _track;
        private readonly Ellipse _thumb;
        private bool _isOn;
        private bool _hover;
        public event Action<bool> Changed;

        public bool IsOn
        {
            get { return _isOn; }
            set
            {
                if (_isOn == value) return;
                _isOn = value;
                UpdateUi();
                if (Changed != null) Changed(_isOn);
            }
        }

        public SwitchControl(bool on)
        {
            _isOn = on;
            Width = 44;
            Height = 24;
            Cursor = Cursors.Hand;
            Focusable = false;
            HorizontalAlignment = HorizontalAlignment.Left;
            SnapsToDevicePixels = true;
            ToolTip = "点击切换";

            _track = new Border
            {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true
            };

            _thumb = new Ellipse
            {
                Width = 18,
                Height = 18,
                Fill = Theme.B(Colors.White),
                Stroke = Theme.B(Color.FromArgb(0x33, 0x00, 0x00, 0x00)),
                StrokeThickness = 1,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect { BlurRadius = 4, ShadowDepth = 1, Direction = 270, Opacity = 0.18, Color = Colors.Black }
            };
            _track.Child = _thumb;
            Child = _track;

            MouseLeftButtonDown += delegate { IsOn = !IsOn; };
            MouseEnter += delegate { _hover = true; UpdateUi(); };
            MouseLeave += delegate { _hover = false; UpdateUi(); };
            UpdateUi();
        }

        private void UpdateUi()
        {
            if (_isOn)
            {
                _track.Background = Theme.B(Theme.Accent);
                _track.BorderBrush = Theme.B(_hover ? Theme.AccentPressed : Theme.AccentHover);
                _thumb.HorizontalAlignment = HorizontalAlignment.Right;
                _thumb.Margin = new Thickness(0, 0, 2, 0);
            }
            else
            {
                _track.Background = Theme.B(_hover ? Color.FromRgb(0xDE, 0xD2, 0xBF) : Color.FromRgb(0xE7, 0xDC, 0xCB));
                _track.BorderBrush = Theme.B(Theme.LineStrong);
                _thumb.HorizontalAlignment = HorizontalAlignment.Left;
                _thumb.Margin = new Thickness(2, 0, 0, 0);
            }
        }
    }
}

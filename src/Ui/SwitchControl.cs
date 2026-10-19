using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OnmyojiTools.Ui
{
    /// <summary>现代开关（纯代码组合控件，无模板，避免 FrameworkElementFactory 名称作用域限制）</summary>
    public class SwitchControl : Border
    {
        private readonly Border _track;
        private readonly Ellipse _thumb;
        private bool _isOn;
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
            Width = 46;
            Height = 24;
            Cursor = Cursors.Hand;
            Focusable = false;
            HorizontalAlignment = HorizontalAlignment.Left;

            _track = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = Theme.BrushFrozen(Theme.TrackOff),
                BorderThickness = new Thickness(0)
            };
            _thumb = new Ellipse
            {
                Width = 18,
                Height = 18,
                Fill = Theme.BrushFrozen(Color.FromRgb(0xC6, 0xCD, 0xD9)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            };
            _track.Child = _thumb;
            Child = _track;

            MouseLeftButtonDown += (s, e) => { IsOn = !IsOn; };
            UpdateUi();
        }

        private void UpdateUi()
        {
            if (_isOn)
            {
                _track.Background = Theme.AccentGradient();
                _thumb.Fill = Theme.BrushFrozen(Colors.White);
                _thumb.HorizontalAlignment = HorizontalAlignment.Right;
                _thumb.Margin = new Thickness(0, 0, 3, 0);
            }
            else
            {
                _track.Background = Theme.BrushFrozen(Theme.TrackOff);
                _thumb.Fill = Theme.BrushFrozen(Color.FromRgb(0x9A, 0xA3, 0xB2));
                _thumb.HorizontalAlignment = HorizontalAlignment.Left;
                _thumb.Margin = new Thickness(3, 0, 0, 0);
            }
        }
    }
}

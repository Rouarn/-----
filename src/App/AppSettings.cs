using System;
using OnmyojiTools.Core;

namespace OnmyojiTools.App
{
    /// <summary>用户可持久化的设置；所有字段在读取和保存时都会规范化。</summary>
    internal sealed class AppSettings
    {
        public const string DefaultGameRoot = @"D:\Netease\yys";
        public const int MinInstances = InputValidation.MinInstances;
        public const int MaxInstances = InputValidation.MaxInstances;
        public const int MinColumns = InputValidation.MinColumns;
        public const int MaxColumns = InputValidation.MaxColumns;

        public string GameRoot { get; set; }
        public int TargetInstances { get; set; }
        public int Columns { get; set; }
        public int MonitorIndex { get; set; }
        public int BackgroundVolumePercent { get; set; }
        public int HotkeyScheme { get; set; }
        public bool AutoArrangeNewWindows { get; set; }
        public bool AutoRestartUnresponsive { get; set; }
        public bool CompactMode { get; set; }
        public bool DimUnfocusedWindows { get; set; }
        public bool DarkTheme { get; set; }
        public bool SoundFocusEnabled { get; set; }

        public AppSettings()
        {
            GameRoot = DefaultGameRoot;
            TargetInstances = 2;
            Columns = 2;
            MonitorIndex = 0;
            BackgroundVolumePercent = 15;
            HotkeyScheme = 0;
            AutoArrangeNewWindows = false;
            AutoRestartUnresponsive = false;
            CompactMode = false;
            DimUnfocusedWindows = false;
            DarkTheme = false;
            SoundFocusEnabled = true;
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(GameRoot)) GameRoot = DefaultGameRoot;
            GameRoot = GameRoot.Trim();
            TargetInstances = Clamp(TargetInstances, MinInstances, MaxInstances);
            Columns = Clamp(Columns, MinColumns, MaxColumns);
            if (MonitorIndex < 0) MonitorIndex = 0;
            BackgroundVolumePercent = Clamp(BackgroundVolumePercent, 0, 100);
            HotkeyScheme = Clamp(HotkeyScheme, 0, 2);
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}

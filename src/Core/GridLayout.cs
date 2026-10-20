using System;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>网格排布的纯计算部分，与 Win32 调用分离，便于自检。</summary>
    internal sealed class GridLayout
    {
        public const int Gap = 8;
        public const int MinimumProbeHeight = 300;
        public const int MaximumProbeHeight = 1000;
        public const int MinimumTargetWidth = 320;

        public NativeMethods.RECT WorkArea { get; private set; }
        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public int CellWidth { get; private set; }
        public int TargetWidth { get; private set; }
        public int StartHeight { get; private set; }

        public static GridLayout Create(int windowCount, int columns, NativeMethods.RECT workArea)
        {
            if (windowCount < 1) windowCount = 1;
            if (columns < 1) columns = 1;
            if (columns > 4) columns = 4;
            if (workArea.IsEmpty)
            {
                workArea = new NativeMethods.RECT
                {
                    Left = 0,
                    Top = 0,
                    Right = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN)),
                    Bottom = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN))
                };
            }

            GridLayout layout = new GridLayout();
            layout.WorkArea = workArea;
            layout.Columns = columns;
            layout.Rows = (windowCount + columns - 1) / columns;
            layout.CellWidth = Math.Max(1, workArea.Width / columns);
            layout.TargetWidth = Math.Max(MinimumTargetWidth, layout.CellWidth - Gap);
            layout.StartHeight = Math.Min(MaximumProbeHeight,
                Math.Max(MinimumProbeHeight, workArea.Height / Math.Max(1, layout.Rows)));
            return layout;
        }

        public int CellX(int column, int actualWidth)
        {
            int x = WorkArea.Left + column * CellWidth + (CellWidth - actualWidth) / 2;
            int max = WorkArea.Right - actualWidth;
            if (x > max) x = max;
            if (x < WorkArea.Left) x = WorkArea.Left;
            return x;
        }

        public int CellY(int row, int actualHeight)
        {
            int y = WorkArea.Top + row * (actualHeight + Gap);
            int max = WorkArea.Bottom - actualHeight;
            if (y > max) y = max;
            if (y < WorkArea.Top) y = WorkArea.Top;
            return y;
        }
    }
}

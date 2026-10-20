using System;
using System.Collections.Generic;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    internal sealed class MonitorInfo
    {
        public int Index { get; set; }
        public NativeMethods.RECT MonitorArea { get; set; }
        public NativeMethods.RECT WorkArea { get; set; }
        public bool IsPrimary { get; set; }

        public string DisplayName
        {
            get
            {
                return "显示器 " + (Index + 1) + " · " + WorkArea.Width + "×" + WorkArea.Height +
                    (IsPrimary ? " · 主屏" : "");
            }
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    internal interface IMonitorService
    {
        IReadOnlyList<MonitorInfo> GetMonitors();
        NativeMethods.RECT GetWorkArea(int index);
    }

    /// <summary>枚举显示器工作区；按主屏优先、再按左上角排序，保证界面顺序稳定。</summary>
    internal sealed class MonitorService : IMonitorService
    {
        public IReadOnlyList<MonitorInfo> GetMonitors()
        {
            List<MonitorInfo> monitors = new List<MonitorInfo>();
            try
            {
                NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                    delegate(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT rect, IntPtr data)
                    {
                        try
                        {
                            NativeMethods.MONITORINFO info = new NativeMethods.MONITORINFO();
                            info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                            if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
                            {
                                info.rcMonitor = rect;
                                info.rcWork = rect;
                                info.dwFlags = 0;
                            }

                            NativeMethods.RECT work = info.rcWork;
                            if (work.IsEmpty) work = info.rcMonitor;
                            monitors.Add(new MonitorInfo
                            {
                                MonitorArea = info.rcMonitor,
                                WorkArea = work,
                                IsPrimary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0
                            });
                        }
                        catch { }
                        return true;
                    }, IntPtr.Zero);
            }
            catch { }

            if (monitors.Count == 0)
            {
                NativeMethods.RECT work;
                if (!NativeMethods.TryGetWorkArea(out work) || work.IsEmpty)
                {
                    work = new NativeMethods.RECT
                    {
                        Left = 0,
                        Top = 0,
                        Right = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN)),
                        Bottom = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN))
                    };
                }
                monitors.Add(new MonitorInfo
                {
                    MonitorArea = work,
                    WorkArea = work,
                    IsPrimary = true
                });
            }

            monitors.Sort(delegate(MonitorInfo a, MonitorInfo b)
            {
                if (a.IsPrimary != b.IsPrimary) return a.IsPrimary ? -1 : 1;
                int left = a.WorkArea.Left.CompareTo(b.WorkArea.Left);
                return left != 0 ? left : a.WorkArea.Top.CompareTo(b.WorkArea.Top);
            });

            for (int i = 0; i < monitors.Count; i++) monitors[i].Index = i;
            return monitors;
        }

        public NativeMethods.RECT GetWorkArea(int index)
        {
            IReadOnlyList<MonitorInfo> monitors = GetMonitors();
            if (monitors.Count == 0) return new NativeMethods.RECT();
            if (index < 0 || index >= monitors.Count) index = 0;
            return monitors[index].WorkArea;
        }
    }
}

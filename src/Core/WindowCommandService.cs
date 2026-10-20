using System;
using System.Collections.Generic;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>窗口级操作：聚焦、最小化、隐藏/恢复，以及老板键的批量隐藏状态。</summary>
    internal interface IWindowCommandService
    {
        bool Focus(IntPtr handle);
        bool Minimize(IntPtr handle);
        bool Hide(IntPtr handle);
        bool Show(IntPtr handle);
        int HideAll(IReadOnlyList<GameWindowInfo> windows);
        int ShowAll();
        int HiddenCount { get; }
    }

    internal sealed class WindowCommandService : IWindowCommandService
    {
        private sealed class HiddenWindow
        {
            public IntPtr Handle;
            public NativeMethods.RECT Bounds;
            public bool WasMinimized;
        }

        private readonly object _gate = new object();
        private readonly List<HiddenWindow> _hidden = new List<HiddenWindow>();

        public int HiddenCount
        {
            get { lock (_gate) return _hidden.Count; }
        }

        public bool Focus(IntPtr handle)
        {
            if (handle == IntPtr.Zero || !NativeMethods.IsWindow(handle)) return false;
            try
            {
                if (NativeMethods.IsIconic(handle))
                    NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
                else
                    NativeMethods.ShowWindow(handle, NativeMethods.SW_SHOW);

                bool ok = NativeMethods.SetForegroundWindow(handle);
                if (!ok)
                {
                    NativeMethods.SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
                    ok = NativeMethods.SetForegroundWindow(handle);
                }
                return ok;
            }
            catch { return false; }
        }

        public bool Minimize(IntPtr handle)
        {
            if (handle == IntPtr.Zero || !NativeMethods.IsWindow(handle)) return false;
            try { return NativeMethods.ShowWindow(handle, NativeMethods.SW_MINIMIZE); }
            catch { return false; }
        }

        public bool Hide(IntPtr handle)
        {
            return HideCore(handle);
        }

        public bool Show(IntPtr handle)
        {
            HiddenWindow state = null;
            lock (_gate)
            {
                for (int i = _hidden.Count - 1; i >= 0; i--)
                {
                    if (_hidden[i].Handle != handle) continue;
                    state = _hidden[i];
                    _hidden.RemoveAt(i);
                    break;
                }
            }
            if (state == null) return false;
            return ShowCore(state);
        }

        public int HideAll(IReadOnlyList<GameWindowInfo> windows)
        {
            if (windows == null) return 0;
            int count = 0;
            for (int i = 0; i < windows.Count; i++)
            {
                if (HideCore(windows[i].Handle)) count++;
            }
            return count;
        }

        public int ShowAll()
        {
            List<HiddenWindow> copy;
            lock (_gate)
            {
                copy = new List<HiddenWindow>(_hidden);
                _hidden.Clear();
            }

            int count = 0;
            for (int i = 0; i < copy.Count; i++)
            {
                if (ShowCore(copy[i])) count++;
            }
            return count;
        }

        private bool HideCore(IntPtr handle)
        {
            if (handle == IntPtr.Zero || !NativeMethods.IsWindow(handle)) return false;

            lock (_gate)
            {
                for (int i = 0; i < _hidden.Count; i++)
                {
                    if (_hidden[i].Handle == handle) return false;
                }

                NativeMethods.RECT bounds;
                if (!NativeMethods.GetWindowRect(handle, out bounds)) bounds = new NativeMethods.RECT();
                bool minimized = NativeMethods.IsIconic(handle);

                try
                {
                    if (!NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE)) return false;
                }
                catch { return false; }

                _hidden.Add(new HiddenWindow
                {
                    Handle = handle,
                    Bounds = bounds,
                    WasMinimized = minimized
                });
                return true;
            }
        }

        private static bool ShowCore(HiddenWindow state)
        {
            if (state == null || !NativeMethods.IsWindow(state.Handle)) return false;
            try
            {
                NativeMethods.ShowWindow(state.Handle, NativeMethods.SW_SHOW);
                if (state.WasMinimized)
                {
                    NativeMethods.ShowWindow(state.Handle, NativeMethods.SW_MINIMIZE);
                }
                else if (!state.Bounds.IsEmpty)
                {
                    NativeMethods.SetWindowPos(state.Handle, IntPtr.Zero,
                        state.Bounds.Left, state.Bounds.Top, state.Bounds.Width, state.Bounds.Height,
                        NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                }
                return true;
            }
            catch { return false; }
        }
    }
}

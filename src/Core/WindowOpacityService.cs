using System;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>
    /// 可选的“非焦点窗口半透明”。会修改窗口 WS_EX_LAYERED 样式，
    /// 某些客户端/反作弊可能不支持或表现异常，因此默认关闭。
    /// </summary>
    internal static class WindowOpacityService
    {
        public static bool SetOpacity(IntPtr handle, byte alpha)
        {
            if (handle == IntPtr.Zero || !NativeMethods.IsWindow(handle)) return false;
            try
            {
                IntPtr style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE);
                long value = style.ToInt64() | NativeMethods.WS_EX_LAYERED;
                NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE, new IntPtr(value));
                return NativeMethods.SetLayeredWindowAttributes(handle, 0, alpha, NativeMethods.LWA_ALPHA);
            }
            catch { return false; }
        }

        public static void Restore(IntPtr handle)
        {
            SetOpacity(handle, 255);
        }
    }
}

using System;
using System.Text;

namespace OnmyojiTools.Interop
{
    /// <summary>按 PID 读取可执行文件路径；失败时返回空字符串，不抛异常。</summary>
    internal static class ProcessPathReader
    {
        public static string TryGetExecutablePath(int processId)
        {
            if (processId <= 0) return "";
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)processId);
                if (handle == IntPtr.Zero) return "";

                StringBuilder buffer = new StringBuilder(1024);
                uint size = (uint)buffer.Capacity;
                if (!NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size)) return "";
                return buffer.ToString();
            }
            catch { return ""; }
            finally
            {
                if (handle != IntPtr.Zero) NativeMethods.CloseHandle(handle);
            }
        }
    }
}

using System;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>识别出的阴阳师窗口</summary>
    internal class YysWindow
    {
        public IntPtr Hwnd;
        public uint Pid;
        public string Title;
        public Native.RECT Rect;
        public bool Focused;
    }
}

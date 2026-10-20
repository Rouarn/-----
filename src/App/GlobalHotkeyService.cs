using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OnmyojiTools.Diagnostics;
using OnmyojiTools.Interop;

namespace OnmyojiTools.App
{
    /// <summary>全局快捷键抽象；UI 冒烟使用假实现，避免测试时占用系统热键。</summary>
    internal interface IGlobalHotkeyService : IDisposable
    {
        event Action BossKeyPressed;
        event Action<int> FocusRequested;
        event Action<int> CycleRequested;

        void Start();
        void Stop();
        void SetScheme(int scheme);
    }

    /// <summary>
    /// 通过隐藏消息窗口注册全局快捷键：
    /// Ctrl+Alt+H 隐藏/恢复，Ctrl+Alt+1..9 聚焦实例，Ctrl+Alt+←/→ 循环切换。
    /// 若某个组合被其他程序占用，只记录失败，不影响其余快捷键。
    /// </summary>
    internal sealed class GlobalHotkeyService : IGlobalHotkeyService
    {
        private const int WmHotkey = 0x0312;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModNoRepeat = 0x4000;
        private const int IdBoss = 1;
        private const int IdFocusFirst = 10;
        private const int IdNext = 20;
        private const int IdPrevious = 21;

        private readonly AppLogger _logger;
        private readonly HashSet<int> _registered = new HashSet<int>();
        private HotkeyWindow _window;
        private bool _disposed;
        private int _scheme;

        public GlobalHotkeyService(AppLogger logger, int scheme = 0)
        {
            _logger = logger;
            _scheme = scheme < 0 || scheme > 2 ? 0 : scheme;
        }

        public event Action BossKeyPressed;
        public event Action<int> FocusRequested;
        public event Action<int> CycleRequested;

        public void Start()
        {
            if (_disposed || _window != null) return;

            try
            {
                _window = new HotkeyWindow(this);
            }
            catch (Exception ex)
            {
                _window = null;
                _logger.Log("全局快捷键初始化失败：" + ex.Message);
                return;
            }

            int ok = 0;
            uint modifiers = Modifiers | ModNoRepeat;
            string prefix = Prefix;
            if (Register(IdBoss, modifiers, 0x48, prefix + "H")) ok++;
            for (int i = 1; i <= 9; i++)
            {
                if (Register(IdFocusFirst + i - 1, modifiers, (uint)(0x30 + i), prefix + i)) ok++;
            }
            if (Register(IdNext, modifiers, 0x27, prefix + "→")) ok++;
            if (Register(IdPrevious, modifiers, 0x25, prefix + "←")) ok++;

            if (ok > 0)
                _logger.Log("全局快捷键已启用（" + SchemeName + "）：" + prefix + "H 隐藏/恢复，" +
                    prefix + "1..9 聚焦，" + prefix + "←/→ 循环。");
            else
                _logger.Log("全局快捷键未能注册，可能已被其他程序占用。");
        }

        private uint Modifiers
        {
            get
            {
                if (_scheme == 1) return ModControl | ModShift;
                if (_scheme == 2) return ModAlt | ModShift;
                return ModControl | ModAlt;
            }
        }

        private string Prefix
        {
            get
            {
                if (_scheme == 1) return "Ctrl+Shift+";
                if (_scheme == 2) return "Alt+Shift+";
                return "Ctrl+Alt+";
            }
        }

        private string SchemeName
        {
            get
            {
                if (_scheme == 1) return "Ctrl+Shift 方案";
                if (_scheme == 2) return "Alt+Shift 方案";
                return "Ctrl+Alt 方案";
            }
        }

        public void SetScheme(int scheme)
        {
            scheme = scheme < 0 || scheme > 2 ? 0 : scheme;
            if (_scheme == scheme) return;
            bool running = _window != null;
            if (running) Stop();
            _scheme = scheme;
            if (running) Start();
        }

        private bool Register(int id, uint modifiers, uint virtualKey, string label)
        {
            try
            {
                if (NativeMethods.RegisterHotKey(_window.Handle, id, modifiers, virtualKey))
                {
                    _registered.Add(id);
                    return true;
                }
                _logger.Log("快捷键注册失败：" + label + "（可能已被占用）");
            }
            catch (Exception ex)
            {
                _logger.Log("快捷键注册失败：" + label + "：" + ex.Message);
            }
            return false;
        }

        private void HandleHotkey(int id)
        {
            try
            {
                if (id == IdBoss)
                {
                    Action handler = BossKeyPressed;
                    if (handler != null) handler();
                }
                else if (id >= IdFocusFirst && id < IdFocusFirst + 9)
                {
                    Action<int> handler = FocusRequested;
                    if (handler != null) handler(id - IdFocusFirst + 1);
                }
                else if (id == IdNext || id == IdPrevious)
                {
                    Action<int> handler = CycleRequested;
                    if (handler != null) handler(id == IdNext ? 1 : -1);
                }
            }
            catch { }
        }

        public void Stop()
        {
            HotkeyWindow window = _window;
            _window = null;
            if (window == null) return;

            foreach (int id in _registered)
            {
                try { NativeMethods.UnregisterHotKey(window.Handle, id); }
                catch { }
            }
            _registered.Clear();

            try { window.DestroyHandle(); }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }

        private sealed class HotkeyWindow : NativeWindow
        {
            private readonly GlobalHotkeyService _owner;

            public HotkeyWindow(GlobalHotkeyService owner)
            {
                _owner = owner;
                CreateHandle(new CreateParams
                {
                    Caption = "OnmyojiTools.Hotkeys",
                    Parent = new IntPtr(-3)
                });
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WmHotkey)
                {
                    _owner.HandleHotkey(m.WParam.ToInt32());
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }
}

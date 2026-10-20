using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace OnmyojiTools.App
{
    internal interface ITrayService : IDisposable
    {
        event Action ShowRequested;
        event Action ToggleBossRequested;
        event Action RefreshRequested;
        event Action ExitRequested;

        void Start();
    }

    /// <summary>托盘入口：显示控制台、隐藏/恢复全部、刷新、退出；不改变关闭窗口的默认行为。</summary>
    internal sealed class TrayService : ITrayService
    {
        private NotifyIcon _icon;
        private ContextMenuStrip _menu;
        private Icon _ownedIcon;

        public event Action ShowRequested;
        public event Action ToggleBossRequested;
        public event Action RefreshRequested;
        public event Action ExitRequested;

        public void Start()
        {
            if (_icon != null) return;

            Icon icon = null;
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(path))
                {
                    _ownedIcon = new Icon(path);
                    icon = _ownedIcon;
                }
            }
            catch { }
            if (icon == null) icon = SystemIcons.Application;

            _menu = new ContextMenuStrip();
            _menu.ShowImageMargin = false;
            _menu.Font = new Font("Microsoft YaHei UI", 9F);
            _menu.Items.Add(MenuItem("显示控制台", delegate { Fire(ShowRequested); }));
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(MenuItem("隐藏 / 恢复全部", delegate { Fire(ToggleBossRequested); }));
            _menu.Items.Add(MenuItem("刷新实例列表", delegate { Fire(RefreshRequested); }));
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(MenuItem("退出", delegate { Fire(ExitRequested); }));

            _icon = new NotifyIcon
            {
                Icon = icon,
                Text = "阴阳师工具",
                Visible = true,
                ContextMenuStrip = _menu
            };
            _icon.DoubleClick += delegate { Fire(ShowRequested); };
        }

        private static ToolStripMenuItem MenuItem(string text, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += delegate { action(); };
            return item;
        }

        private static void Fire(Action handler)
        {
            if (handler == null) return;
            try { handler(); }
            catch { }
        }

        public void Dispose()
        {
            if (_icon != null)
            {
                try { _icon.Visible = false; }
                catch { }
                _icon.Dispose();
                _icon = null;
            }
            if (_menu != null)
            {
                _menu.Dispose();
                _menu = null;
            }
            if (_ownedIcon != null)
            {
                _ownedIcon.Dispose();
                _ownedIcon = null;
            }
        }
    }
}

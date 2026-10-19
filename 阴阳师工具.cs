using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Markup;
using System.Windows.Threading;

// ============================================================================
// 阴阳师工具（WPF 现代化版）
// 功能：多开 / 窗口排列 / 声音跟随聚焦 / 一键关闭（全部保留 WinForms 版逻辑）
// 界面：深色现代化卡片式（借鉴 Material Design：卡片分区、圆角、强调色、悬停反馈）
// 编译：csc /target:winexe（见 build.bat），无第三方依赖，单 exe
// ============================================================================
namespace OnmyojiTools
{
    /// <summary>Win32 原生 API 声明（.NET Framework，无需额外运行时）</summary>
    internal static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, IntPtr dwExtraInfo);

        [DllImport("shcore.dll")]
        public static extern int SetProcessDpiAwareness(int value);

        public const int SW_RESTORE = 9;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SPI_GETWORKAREA = 0x0030;
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const uint BM_CLICK = 0x00F5;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int L, T, R, B; }
    }

    // ==================== WASAPI 按进程音量控制 ====================
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IMMDeviceCollection ppDevices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        int GetDevice(string pwstrId, out IMMDevice ppDevice);
        int RegisterEndpointNotificationCallback(object pClient);
        int UnregisterEndpointNotificationCallback(object pClient);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        int GetCount(out uint pcCount);
        int Item(uint nDevice, out IMMDevice ppDevice);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
        int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
        int GetId(out string ppstrId);
        int GetState(out int pdwState);
    }

    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2
    {
        int GetAudioSessionControl(IntPtr AudioSessionGuid, int StreamFlags, out object ppSessionControl);
        int GetSimpleAudioVolume(IntPtr AudioSessionGuid, int StreamFlags, out ISimpleAudioVolume AudioVolume);
        int GetSessionEnumerator(out IAudioSessionEnumerator ppSessionList);
        int RegisterSessionNotification(object pClient);
        int UnregisterSessionNotification(object pClient);
        int RegisterDuckNotification(string sessionId, object pClient);
        int UnregisterDuckNotification(string sessionId, object pClient);
    }

    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionEnumerator
    {
        int GetCount(out int pcCount);
        int GetSession(int index, out IAudioSessionControl ppSession);
    }

    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl
    {
        int GetState(out int pRetVal);
        int GetDisplayName(out IntPtr ppRetVal);
        int SetDisplayName(string pValue, ref Guid EventContext);
        int GetIconPath(out IntPtr ppRetVal);
        int SetIconPath(string pValue, ref Guid EventContext);
        int GetGroupingParam(out Guid pGroupingParam);
        int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        int RegisterAudioSessionNotification(object pNewNotifications);
        int UnregisterAudioSessionNotification(object pNewNotifications);
    }

    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl2
    {
        int GetState(out int pRetVal);
        int GetDisplayName(out IntPtr ppRetVal);
        int SetDisplayName(string pValue, ref Guid EventContext);
        int GetIconPath(out IntPtr ppRetVal);
        int SetIconPath(string pValue, ref Guid EventContext);
        int GetGroupingParam(out Guid pGroupingParam);
        int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        int RegisterAudioSessionNotification(object pNewNotifications);
        int UnregisterAudioSessionNotification(object pNewNotifications);
        int GetSessionIdentifier(out IntPtr ppRetVal);
        int GetSessionInstanceIdentifier(out IntPtr ppRetVal);
        int GetProcessId(out uint pdwProcessId);
        int IsSystemSoundsSession();
        int SetDuckingPreference(bool bDuckingPreference);
    }

    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISimpleAudioVolume
    {
        int SetMasterVolume(float fLevel, ref Guid EventContext);
        int GetMasterVolume(out float pfLevel);
        int SetMute(bool bMute, ref Guid EventContext);
        int GetMute(out bool pbMute);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute(bool bMute, ref Guid pguidEventContext);
        int GetMute(out bool pbMute);
        int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        int VolumeStepUp(ref Guid pguidEventContext);
        int VolumeStepDown(ref Guid pguidEventContext);
        int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        int GetVolumeRange(out float pflMinVolumeDB, out float pflMaxVolumeDB, out float pflIncrementDB);
    }

    /// <summary>枚举系统音频会话（提供按 PID 调整音量的能力）</summary>
    internal static class AudioApi
    {
        public static string LogFile = "";

        public static void Log(string msg)
        {
            try
            {
                if (string.IsNullOrEmpty(LogFile)) return;
                // 日志轮转：超过 2MB 直接重建，避免文件无限膨胀拖慢磁盘 IO
                if (File.Exists(LogFile))
                {
                    FileInfo fi = new FileInfo(LogFile);
                    if (fi.Length > 2 * 1024 * 1024)
                        File.WriteAllText(LogFile, "");
                }
                File.AppendAllText(LogFile, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }

        public sealed class SessionInfo
        {
            public uint Pid;
            public string ProcessName;
            public ISimpleAudioVolume Volume;
        }

        public static List<SessionInfo> EnumerateSessions()
        {
            List<SessionInfo> result = new List<SessionInfo>();
            try
            {
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                IMMDevice device;
                int hr = enumerator.GetDefaultAudioEndpoint(0, 0, out device);
                Log("EnumSessions: GetDefaultAudioEndpoint hr=0x" + hr.ToString("X8"));
                if (hr == 0) DumpDeviceSessions(device, "default", result);

                IMMDeviceCollection coll;
                hr = enumerator.EnumAudioEndpoints(0, 0x0F, out coll);
                Log("EnumSessions: EnumAudioEndpoints hr=0x" + hr.ToString("X8"));
                if (hr == 0)
                {
                    uint n;
                    if (coll.GetCount(out n) == 0)
                    {
                        Log("EnumSessions: render device count=" + n);
                        for (uint i = 0; i < n; i++)
                        {
                            IMMDevice dev;
                            if (coll.Item(i, out dev) != 0) continue;
                            DumpDeviceSessions(dev, "dev" + i, result);
                        }
                    }
                }
            }
            catch (Exception ex) { Log("EnumSessions: EXCEPTION " + ex.GetType().Name + ": " + ex.Message); }
            return result;
        }

        private static void DumpDeviceSessions(IMMDevice device, string label, List<SessionInfo> result)
        {
            try
            {
                IntPtr mgrPtr;
                Guid mgrIid = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
                int hr = device.Activate(ref mgrIid, 7, IntPtr.Zero, out mgrPtr);
                Log("EnumSessions[" + label + "]: Activate hr=0x" + hr.ToString("X8"));
                if (hr != 0) return;

                IAudioSessionManager2 mgr = (IAudioSessionManager2)Marshal.GetObjectForIUnknown(mgrPtr);
                IAudioSessionEnumerator sessions;
                hr = mgr.GetSessionEnumerator(out sessions);
                Log("EnumSessions[" + label + "]: GetSessionEnumerator hr=0x" + hr.ToString("X8"));
                if (hr != 0) return;

                int count;
                if (sessions.GetCount(out count) != 0) return;
                Log("EnumSessions[" + label + "]: count=" + count);
                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl ctl;
                    if (sessions.GetSession(i, out ctl) != 0) continue;
                    IAudioSessionControl2 ctl2 = (IAudioSessionControl2)ctl;
                    uint pid;
                    int hrPid = ctl2.GetProcessId(out pid);
                    if (hrPid != 0)
                    {
                        Log("EnumSessions[" + label + "]: session[" + i + "] GetProcessId failed hr=0x" + hrPid.ToString("X8"));
                        continue;
                    }
                    ISimpleAudioVolume vol = (ISimpleAudioVolume)ctl;
                    string name = "";
                    try { if (pid != 0) name = Process.GetProcessById((int)pid).ProcessName; } catch { }
                    Log("EnumSessions[" + label + "]: session[" + i + "] pid=" + pid + " proc=" + name);
                    result.Add(new SessionInfo { Pid = pid, ProcessName = name, Volume = vol });
                }
            }
            catch (Exception ex) { Log("EnumSessions[" + label + "]: EXCEPTION " + ex.GetType().Name + ": " + ex.Message); }
        }

        public static float GetCurrentVolume(ISimpleAudioVolume vol)
        {
            float v = 1.0f;
            try { vol.GetMasterVolume(out v); } catch { }
            return v;
        }

        /// <summary>读取系统总音量（默认渲染端点的主音量标量 0~1）</summary>
        public static float GetSystemMasterVolume()
        {
            float level = 1.0f;
            try
            {
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                IMMDevice device;
                int hr = enumerator.GetDefaultAudioEndpoint(0, 0, out device);
                if (hr != 0) { Log("GetSysVol: GetDefaultAudioEndpoint hr=0x" + hr.ToString("X8")); return level; }
                IntPtr epPtr;
                Guid epIid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
                hr = device.Activate(ref epIid, 7, IntPtr.Zero, out epPtr);
                if (hr != 0) { Log("GetSysVol: Activate hr=0x" + hr.ToString("X8")); return level; }
                IAudioEndpointVolume ep = (IAudioEndpointVolume)Marshal.GetObjectForIUnknown(epPtr);
                hr = ep.GetMasterVolumeLevelScalar(out level);
                if (hr != 0) Log("GetSysVol: GetMasterVolumeLevelScalar hr=0x" + hr.ToString("X8"));
            }
            catch (Exception ex) { Log("GetSysVol: EXCEPTION " + ex.GetType().Name + ": " + ex.Message); }
            return level;
        }

        public static void SetVolume(ISimpleAudioVolume vol, float level)
        {
            try
            {
                Guid ctx = Guid.Empty;
                vol.SetMasterVolume(Math.Max(0f, Math.Min(1f, level)), ref ctx);
            }
            catch { }
        }
    }

    /// <summary>声音跟随聚焦控制器：前台聚焦窗口恢复为系统总音量响度，失焦窗口声音调小；全部失焦则全部调小</summary>
    internal class SoundFocusController
    {
        private const float LOW_VOLUME = 0.15f;
        private const float FOCUS_VOLUME = 1.0f;
        // 记录每个 pid 上次设置的目标音量（float），用于避免无谓重复设置
        private readonly Dictionary<uint, float> _lastSet = new Dictionary<uint, float>();

        public void Scan()
        {
            HashSet<uint> targets = CollectTargetPids();
            if (targets.Count == 0)
            {
                AudioApi.Log("Scan: no target processes");
                return;
            }
            AudioApi.Log("Scan: targets=" + string.Join(",", targets));

            IntPtr fg = Native.GetForegroundWindow();
            uint fgPid = 0;
            if (fg != IntPtr.Zero) Native.GetWindowThreadProcessId(fg, out fgPid);
            AudioApi.Log("Scan: fgPid=" + fgPid);

            // 仅在聚焦时实时获取系统总音量作为基准（切换焦点时读取一次），失焦时直接调15%无需再读
            float sysVol = 1.0f;
            if (fgPid > 0) sysVol = AudioApi.GetSystemMasterVolume();
            AudioApi.Log("Scan: fgPid=" + fgPid + " sysVol=" + sysVol);

            List<AudioApi.SessionInfo> sessions = AudioApi.EnumerateSessions();
            Dictionary<uint, List<ISimpleAudioVolume>> byPid = new Dictionary<uint, List<ISimpleAudioVolume>>();
            foreach (AudioApi.SessionInfo s in sessions)
            {
                List<ISimpleAudioVolume> list;
                if (!byPid.TryGetValue(s.Pid, out list)) { list = new List<ISimpleAudioVolume>(); byPid[s.Pid] = list; }
                list.Add(s.Volume);
            }
            AudioApi.Log("Scan: sessions=" + sessions.Count + " byPidKeys=" + string.Join(",", byPid.Keys));

            foreach (uint pid in targets)
            {
                List<ISimpleAudioVolume> vols;
                if (!byPid.TryGetValue(pid, out vols) || vols.Count == 0)
                {
                    AudioApi.Log("Scan: pid=" + pid + " NO audio session -> skip");
                    continue;
                }

                bool focused = (pid == fgPid);
                float target = focused ? FOCUS_VOLUME : LOW_VOLUME;

                float last;
                if (!_lastSet.TryGetValue(pid, out last) || Math.Abs(last - target) > 0.005f)
                {
                    foreach (ISimpleAudioVolume v in vols) AudioApi.SetVolume(v, target);
                    _lastSet[pid] = target;
                    AudioApi.Log("Scan: pid=" + pid + (focused ? " -> FOCUS(sysVol=" + sysVol + ") " : " -> LOW ") + target);
                }
            }
        }

        private static HashSet<uint> CollectTargetPids()
        {
            HashSet<uint> set = new HashSet<uint>();
            try
            {
                foreach (Process p in Process.GetProcessesByName("onmyoji")) set.Add((uint)p.Id);
                foreach (Process p in Process.GetProcessesByName("yys")) set.Add((uint)p.Id);
            }
            catch { }
            return set;
        }
    }

    /// <summary>识别出的阴阳师窗口</summary>
    internal class YysWindow
    {
        public IntPtr Hwnd;
        public uint Pid;
        public string Title;
        public Native.RECT Rect;
        public bool Focused;
    }

    // ==================== 主题与控件工厂（现代化深色 Material 风格） ====================
    internal static class Theme
    {
        // 色板（赛博霓虹深色：近黑基底 + 锌色阶卡片 + 电光青蓝强调）
        public static readonly Color BgMain = Color.FromRgb(0x0A, 0x0B, 0x0E);
        public static readonly Color BgCard = Color.FromRgb(0x15, 0x16, 0x1C);
        public static readonly Color BgInput = Color.FromRgb(0x0E, 0x0F, 0x14);
        public static readonly Color BgHover = Color.FromRgb(0x1E, 0x20, 0x28);
        public static readonly Color BgSel = Color.FromRgb(0x16, 0x21, 0x2E);
        public static readonly Color BorderLine = Color.FromRgb(0x22, 0x24, 0x2E);
        public static readonly Color BorderHover = Color.FromRgb(0x3E, 0x42, 0x50);
        public static readonly Color TextMain = Color.FromRgb(0xF5, 0xF6, 0xFA);
        public static readonly Color TextSub = Color.FromRgb(0x9A, 0xA0, 0xB0);
        public static readonly Color Accent = Color.FromRgb(0x22, 0xD3, 0xEE);
        public static readonly Color AccentLight = Color.FromRgb(0x67, 0xE8, 0xF9);
        public static readonly Color AccentDark = Color.FromRgb(0x08, 0x91, 0xB2);
        public static readonly Color AccentBright = Color.FromRgb(0xA5, 0xF3, 0xFC);
        public static readonly Color Danger = Color.FromRgb(0xF4, 0x3F, 0x5E);
        public static readonly Color DangerDark = Color.FromRgb(0xBE, 0x12, 0x3C);
        public static readonly Color DangerBright = Color.FromRgb(0xFB, 0x71, 0x85);
        public static readonly Color Success = Color.FromRgb(0x34, 0xD3, 0x99);
        public static readonly Color TrackOff = Color.FromRgb(0x33, 0x37, 0x40);
        public static readonly Color NavHover = Color.FromRgb(0x18, 0x1B, 0x23);
        public static readonly Color NavSel = Color.FromRgb(0x0F, 0x25, 0x30);

        public static readonly FontFamily FontUI = new FontFamily("Segoe UI");
        public static readonly FontFamily FontYH = new FontFamily("Microsoft YaHei UI");

        public static Brush Brush(Color c) { return new SolidColorBrush(c); }
        public static Brush BrushFrozen(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        // 垂直渐变画刷（霓虹主色系）
        public static Brush AccentGradient()
        {
            LinearGradientBrush g = new LinearGradientBrush(AccentLight, AccentDark, 90d);
            g.Freeze();
            return g;
        }
        public static Brush AccentGradientHover()
        {
            LinearGradientBrush g = new LinearGradientBrush(AccentBright, Color.FromRgb(0x0E, 0x7F, 0x9E), 90d);
            g.Freeze();
            return g;
        }
        public static Brush AccentGradientPressed()
        {
            LinearGradientBrush g = new LinearGradientBrush(Color.FromRgb(0x06, 0x6E, 0x88), Color.FromRgb(0x05, 0x5C, 0x74), 90d);
            g.Freeze();
            return g;
        }
        public static Brush DangerGradient()
        {
            LinearGradientBrush g = new LinearGradientBrush(DangerBright, DangerDark, 90d);
            g.Freeze();
            return g;
        }
        public static Brush DangerGradientHover()
        {
            LinearGradientBrush g = new LinearGradientBrush(Color.FromRgb(0xFB, 0x8F, 0xA0), Color.FromRgb(0xCF, 0x2E, 0x4C), 90d);
            g.Freeze();
            return g;
        }
        public static Brush DangerGradientPressed()
        {
            LinearGradientBrush g = new LinearGradientBrush(Color.FromRgb(0x99, 0x0E, 0x30), Color.FromRgb(0x86, 0x0C, 0x2A), 90d);
            g.Freeze();
            return g;
        }

        public static DropShadowEffect CardShadow()
        {
            return new DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 6,
                Opacity = 0.42,
                Color = Colors.Black
            };
        }

        // ---------- 通用文本 ----------
        public static TextBlock Label(string text, double size = 12.5, bool bold = false, Brush fg = null)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = FontYH,
                FontSize = size,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = fg ?? BrushFrozen(TextSub),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // ---------- 卡片容器 ----------
        public static Border Card(UIElement child)
        {
            Border b = new Border
            {
                Background = BrushFrozen(BgCard),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18, 16, 18, 16),
                Effect = CardShadow(),
                Child = child
            };
            return b;
        }

        public static Grid CardHeader(string title, string subtitle, UIElement extra = null)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel sp = new StackPanel();
            TextBlock t = new TextBlock
            {
                Text = title,
                FontFamily = FontYH,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFrozen(TextMain)
            };
            sp.Children.Add(t);
            if (!string.IsNullOrEmpty(subtitle))
            {
                sp.Children.Add(new TextBlock
                {
                    Text = subtitle,
                    FontFamily = FontYH,
                    FontSize = 11,
                    Foreground = BrushFrozen(TextSub),
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            Grid.SetColumn(sp, 0);
            g.Children.Add(sp);
            if (extra != null)
            {
                Grid.SetColumn(extra, 1);
                g.Children.Add(extra);
            }
            return g;
        }

        // ---------- 按钮 ----------
        public static Button MakeButton(string text, Action onClick, bool primary = false, bool danger = false)
        {
            Button b = new Button
            {
                Content = text,
                FontFamily = FontYH,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Height = 38,
                Padding = new Thickness(16, 0, 16, 0),
                Cursor = Cursors.Hand,
                Focusable = false,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            b.BorderThickness = new Thickness(0);
            b.BorderBrush = Brushes.Transparent;
            if (primary)
            {
                b.Background = AccentGradient();
                b.Foreground = BrushFrozen(Colors.White);
                b.Template = ButtonTemplate(AccentGradientHover(), AccentGradientPressed());
            }
            else if (danger)
            {
                b.Background = DangerGradient();
                b.Foreground = BrushFrozen(Colors.White);
                b.Template = ButtonTemplate(DangerGradientHover(), DangerGradientPressed());
            }
            else
            {
                b.Background = BrushFrozen(Color.FromArgb(0, 0, 0, 0)); // 透明
                b.Foreground = BrushFrozen(TextMain);
                b.BorderThickness = new Thickness(1);
                b.BorderBrush = BrushFrozen(BorderLine);
                b.Template = ButtonTemplate(BrushFrozen(BgHover), BrushFrozen(BgSel));
            }

            if (onClick != null) b.Click += (s, e) => onClick();
            return b;
        }

        private static ControlTemplate ButtonTemplate(Brush hoverBg, Brush pressedBg)
        {
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bd.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            bd.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));

            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetValue(ContentPresenter.MarginProperty, new Thickness(12, 0, 12, 0));
            bd.AppendChild(cp);

            ControlTemplate t = new ControlTemplate(typeof(Button));
            t.VisualTree = bd;

            Trigger hov = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(Button.BackgroundProperty, hoverBg));
            t.Triggers.Add(hov);

            Trigger prs = new Trigger { Property = Button.IsPressedProperty, Value = true };
            prs.Setters.Add(new Setter(Button.BackgroundProperty, pressedBg));
            t.Triggers.Add(prs);

            Trigger dis = new Trigger { Property = Button.IsEnabledProperty, Value = false };
            dis.Setters.Add(new Setter(Button.BackgroundProperty, BrushFrozen(Color.FromRgb(0x1C, 0x21, 0x2A))));
            dis.Setters.Add(new Setter(Button.ForegroundProperty, BrushFrozen(Color.FromRgb(0x5A, 0x61, 0x6E))));
            t.Triggers.Add(dis);

            return t;
        }

        // ---------- 输入框 ----------
        public static TextBox MakeTextBox(string def, int width = 62, bool readOnly = false)
        {
            TextBox t = new TextBox
            {
                Text = def,
                FontFamily = FontYH,
                FontSize = 12.5,
                Width = width,
                Height = 38,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Background = BrushFrozen(BgInput),
                Foreground = BrushFrozen(TextMain),
                BorderBrush = BrushFrozen(BorderLine),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 0, 10, 0),
                IsReadOnly = readOnly,
                CaretBrush = BrushFrozen(AccentLight)
            };
            t.Template = TextBoxTemplate();
            return t;
        }

        private static ControlTemplate TextBoxTemplate()
        {
            // 注意：不能用 FrameworkElementFactory 设置 PART_ContentHost 名称——
            // 纯代码工厂的名称不会注册进模板 NameScope，TextBox 内部 GetTemplateChild("PART_ContentHost")
            // 会返回 null，导致文本框渲染出边框背景但文本/光标完全不显示。
            // 因此用 XamlReader 解析 XAML 构建模板，x:Name 才能正确注册。
            const string xaml =
@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                   xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                   TargetType='TextBox'>
    <Border CornerRadius='12'
            Background='{TemplateBinding Background}'
            BorderBrush='{TemplateBinding BorderBrush}'
            BorderThickness='{TemplateBinding BorderThickness}'
            Padding='10,0,10,0'>
        <ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center'/>
    </Border>
    <ControlTemplate.Triggers>
        <Trigger Property='IsKeyboardFocusWithin' Value='True'>
            <Setter Property='BorderBrush' Value='#22D3EE'/>
            <Setter Property='BorderThickness' Value='1.5'/>
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(xaml);
        }

        // ---------- 现代开关（纯代码组合控件，无模板，避免 FrameworkElementFactory 名称作用域限制） ----------
        public sealed class SwitchControl : Border
        {
            private readonly Border _track;
            private readonly System.Windows.Shapes.Ellipse _thumb;
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
                    Background = BrushFrozen(TrackOff),
                    BorderThickness = new Thickness(0)
                };
                _thumb = new System.Windows.Shapes.Ellipse
                {
                    Width = 18,
                    Height = 18,
                    Fill = BrushFrozen(Color.FromRgb(0xC6, 0xCD, 0xD9)),
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
                    _track.Background = AccentGradient();
                    _thumb.Fill = BrushFrozen(Colors.White);
                    _thumb.HorizontalAlignment = HorizontalAlignment.Right;
                    _thumb.Margin = new Thickness(0, 0, 3, 0);
                }
                else
                {
                    _track.Background = BrushFrozen(TrackOff);
                    _thumb.Fill = BrushFrozen(Color.FromRgb(0x9A, 0xA3, 0xB2));
                    _thumb.HorizontalAlignment = HorizontalAlignment.Left;
                    _thumb.Margin = new Thickness(3, 0, 0, 0);
                }
            }
        }

        // ---------- 徽标（胶囊） ----------
        public static Border Badge(string text, Brush bg, Brush fg)
        {
            Border b = new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 3, 10, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = text,
                    FontFamily = FontYH,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = fg
                }
            };
            return b;
        }
    }

    /// <summary>主窗口（WPF 现代化深色卡片式界面）</summary>
    public class MainWindow : Window
    {
        // ---------- 常量 ----------
        private const string DEFAULT_YYS_ROOT = @"D:\Netease\yys";
        private const string PROC_NAME = "onmyoji";
        private const int INSTANCE_INTERVAL_MS = 8000;
        private const int PROBE_STEP_MS = 400;
        private string _yysRoot = DEFAULT_YYS_ROOT;

        // ---------- 控件 ----------
        private TextBox txtRoot;
        private TextBox txtOpenNum;
        private TextBox txtColsNum;
        private TextBlock lblRunning;
        private TextBlock lblWinCount;
        private Button btnOpen, btnArrange, btnRefresh, btnCloseAll, btnBrowse;
        private Button btnPin;
        private ListView listWins;
        private TextBox txtLog;
        private DispatcherTimer timerSound;
        private SoundFocusController _sf;
        private Grid pageOpen, pageArrange, pageSound, pageAbout;
        private Border navOpen, navArrange, navSound, navAbout;

        public MainWindow()
        {
            Title = "阴阳师工具 - 多开 & 窗口排列";
            Width = 1080;
            Height = 760;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = Theme.FontYH;

            BuildUi();
            LoadConfig();

            // 声音跟随聚焦：常驻监控
            AudioApi.LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio_log.txt");
            _sf = new SoundFocusController();
            timerSound = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timerSound.Tick += TimerSound_Tick;
            Closed += delegate { timerSound.Stop(); };
            if (swSound != null && swSound.IsOn) timerSound.Start();

            // 启动即激活前台，避免无边框窗口首次点击只激活不触发按钮
            Loaded += (s, e) =>
            {
                Activate();
                Topmost = false;
            };
        }

        // ==================== 界面构建 ====================
        private void BuildUi()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 根容器（圆角 + 阴影 + 微渐变）
            Border shell = new Border
            {
                CornerRadius = new CornerRadius(16),
                ClipToBounds = true,
                Effect = new DropShadowEffect { BlurRadius = 32, ShadowDepth = 10, Opacity = 0.55, Color = Colors.Black }
            };
            LinearGradientBrush bg = new LinearGradientBrush(
                Color.FromRgb(0x10, 0x22, 0x2C), Theme.BgMain, 90d);
            bg.Freeze();
            shell.Background = bg;
            Content = shell;

            // ==================== 标题栏 ====================
            Grid bar = new Grid();
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.MouseLeftButtonDown += TitleBar_MouseDown;

            // 左侧：图标 + 标题
            StackPanel titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 0, 0) };
            Border iconBox = new Border
            {
                Width = 30, Height = 30,
                CornerRadius = new CornerRadius(9),
                Background = Theme.AccentGradient(),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            iconBox.Child = new TextBlock
            {
                Text = "\uE7FC",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleSp.Children.Add(iconBox);
            titleSp.Children.Add(new TextBlock
            {
                Text = "阴阳师工具",
                FontFamily = Theme.FontYH,
                FontSize = 16.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.BrushFrozen(Theme.TextMain),
                VerticalAlignment = VerticalAlignment.Center
            });
            titleSp.Children.Add(new TextBlock
            {
                Text = "  多开 · 窗口排列 · 声随聚焦",
                FontFamily = Theme.FontYH,
                FontSize = 11.5,
                Foreground = Theme.BrushFrozen(Theme.TextSub),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 2, 0, 0)
            });
            Grid.SetColumn(titleSp, 0);
            bar.Children.Add(titleSp);

            // 运行状态徽标
            lblRunning = Theme.Label("当前运行实例：--", 11.5, true, Theme.BrushFrozen(Theme.Success));
            Border runBadge = new Border
            {
                Background = Theme.BrushFrozen(Color.FromRgb(0x1B, 0x2E, 0x26)),
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(14, 5, 14, 5),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Child = lblRunning
            };
            Grid.SetColumn(runBadge, 1);
            bar.Children.Add(runBadge);

            // 置顶开关（防被其它窗口遮挡导致点击失效）
            btnPin = new Button
            {
                Content = "\uE840", // Segoe MDL2 Assets: Pin（图钉）
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Width = 46,
                Height = 34,
                Foreground = Theme.BrushFrozen(Theme.TextSub),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = Theme.BrushFrozen(Theme.BorderLine),
                Cursor = Cursors.Hand,
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 2, 0)
            };
            btnPin.Template = TitleButtonTextTemplate();
            btnPin.ToolTip = "窗口置顶：开启后工具窗口始终显示在最前，点击不会被其它窗口遮挡";
            btnPin.Click += (s, e) => TogglePin();
            Grid.SetColumn(btnPin, 2);
            bar.Children.Add(btnPin);

            Button btnMin = TitleButton("\uE921", false);
            btnMin.Margin = new Thickness(0, 0, 2, 0);
            btnMin.Click += (s, e) => WindowState = WindowState.Minimized;
            Grid.SetColumn(btnMin, 3);
            bar.Children.Add(btnMin);

            Button btnClose = TitleButton("\uE8BB", true);
            btnClose.Margin = new Thickness(0, 0, 12, 0);
            btnClose.Click += (s, e) => Close();
            Grid.SetColumn(btnClose, 4);
            bar.Children.Add(btnClose);

            Grid.SetRow(bar, 0);
            root.Children.Add(bar);

            // 标题栏分隔线
            Border sep = new Border
            {
                Height = 1,
                Background = Theme.BrushFrozen(Color.FromRgb(0x22, 0x28, 0x34)),
                Margin = new Thickness(20, 0, 20, 0),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetRow(sep, 0);
            root.Children.Add(sep);

            // ==================== 主体：左导航 + 右内容 ====================
            Grid body = new Grid { Margin = new Thickness(18, 10, 18, 16) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(196) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ---- 左侧导航 ----
            StackPanel nav = new StackPanel { Margin = new Thickness(4, 6, 0, 0) };
            TextBlock navTitle = Theme.Label("功能导航", 10.5);
            navTitle.Margin = new Thickness(14, 0, 0, 10);
            nav.Children.Add(navTitle);

            navOpen = NavItem("\uE8C8", "多开管理", true, () => SwitchNav(navOpen));
            nav.Children.Add(navOpen);
            navArrange = NavItem("\uE9E9", "窗口排列", false, () => SwitchNav(navArrange));
            nav.Children.Add(navArrange);
            navSound = NavItem("\uE767", "声音控制", false, () => SwitchNav(navSound));
            nav.Children.Add(navSound);
            navAbout = NavItem("\uE946", "关于", false, () => SwitchNav(navAbout));
            nav.Children.Add(navAbout);

            Grid.SetColumn(nav, 0);
            body.Children.Add(nav);

            // 导航分隔线
            Border navSep = new Border
            {
                Width = 1,
                Background = Theme.BrushFrozen(Color.FromRgb(0x22, 0x28, 0x34)),
                Margin = new Thickness(0, 2, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(navSep, 0);
            body.Children.Add(navSep);

            // ---- 右侧：页面 + 日志 ----
            Grid right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(172) });

            // 页面容器（4 页切换）
            Grid pageHost = new Grid();
            pageOpen = BuildPageOpen();
            pageArrange = BuildPageArrange();
            pageSound = BuildPageSound();
            pageAbout = BuildPageAbout();
            pageHost.Children.Add(pageOpen);
            pageHost.Children.Add(pageArrange);
            pageHost.Children.Add(pageSound);
            pageHost.Children.Add(pageAbout);
            pageArrange.Visibility = Visibility.Collapsed;
            pageSound.Visibility = Visibility.Collapsed;
            pageAbout.Visibility = Visibility.Collapsed;
            Grid.SetRow(pageHost, 0);
            right.Children.Add(pageHost);

            // 日志卡片
            Border cardLog = BuildLogCard();
            Grid.SetRow(cardLog, 2);
            right.Children.Add(cardLog);

            Grid.SetColumn(right, 2);
            body.Children.Add(right);

            Grid.SetRow(body, 1);
            root.Children.Add(body);

            shell.Child = root;
        }

        // ---------- 导航项 ----------
        private Border NavItem(string glyph, string text, bool selected, Action onClick)
        {
            Grid g = new Grid { Margin = new Thickness(2, 0, 6, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border indicator = new Border
            {
                Width = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = Theme.BrushFrozen(Theme.AccentLight),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = selected ? Visibility.Visible : Visibility.Collapsed
            };
            Grid.SetColumn(indicator, 0);
            g.Children.Add(indicator);

            TextBlock icon = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = Theme.BrushFrozen(selected ? Theme.AccentLight : Theme.TextSub),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 1);
            g.Children.Add(icon);

            TextBlock label = Theme.Label(text, 13, true, Theme.BrushFrozen(selected ? Theme.TextMain : Theme.TextSub));
            Grid.SetColumn(label, 2);
            g.Children.Add(label);

            Border b = new Border
            {
                Height = 44,
                CornerRadius = new CornerRadius(12),
                Background = Theme.BrushFrozen(selected ? Theme.NavSel : Color.FromArgb(0, 0, 0, 0)),
                Cursor = Cursors.Hand,
                Child = g,
                Tag = selected
            };
            b.MouseEnter += (s, e) => { if (!(bool)b.Tag) b.Background = Theme.BrushFrozen(Theme.NavHover); };
            b.MouseLeave += (s, e) => { if (!(bool)b.Tag) b.Background = Theme.BrushFrozen(Color.FromArgb(0, 0, 0, 0)); };
            if (onClick != null) b.MouseLeftButtonDown += (s, e) => onClick();
            return b;
        }

        private void SwitchNav(Border active)
        {
            Border[] navs = new[] { navOpen, navArrange, navSound, navAbout };
            Grid[] pages = new[] { pageOpen, pageArrange, pageSound, pageAbout };
            for (int i = 0; i < navs.Length; i++)
            {
                bool on = navs[i] == active;
                navs[i].Tag = on;
                navs[i].Background = Theme.BrushFrozen(on ? Theme.NavSel : Color.FromArgb(0, 0, 0, 0));
                pages[i].Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                Grid g = navs[i].Child as Grid;
                if (g != null)
                {
                    TextBlock icon = g.Children[1] as TextBlock;
                    TextBlock label = g.Children[2] as TextBlock;
                    Border indicator = g.Children[0] as Border;
                    if (icon != null) icon.Foreground = Theme.BrushFrozen(on ? Theme.AccentLight : Theme.TextSub);
                    if (label != null) label.Foreground = Theme.BrushFrozen(on ? Theme.TextMain : Theme.TextSub);
                    if (indicator != null) indicator.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        // ---------- 页面：多开管理 ----------
        private Grid BuildPageOpen()
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 卡片1：多开设置
            StackPanel spOpen = new StackPanel();
            spOpen.Children.Add(Theme.CardHeader("多开设置", "从工作目录启动 onmyoji.exe，实例间隔 8 秒"));

            Grid rowOpen = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            rowOpen.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock lbOpen = Theme.Label("目标开数：", 12.5, true, Theme.BrushFrozen(Theme.TextMain));
            Grid.SetColumn(lbOpen, 0);
            rowOpen.Children.Add(lbOpen);
            txtOpenNum = Theme.MakeTextBox("2", 72);
            Grid.SetColumn(txtOpenNum, 1);
            rowOpen.Children.Add(txtOpenNum);
            btnOpen = Theme.MakeButton("开始多开", () => { var _ = BtnOpen_Click(); }, true);
            Grid.SetColumn(btnOpen, 3);
            rowOpen.Children.Add(btnOpen);
            spOpen.Children.Add(rowOpen);

            TextBlock hintOpen = Theme.Label("目标开数范围为 1-10，自动补足当前缺口", 11);
            hintOpen.Margin = new Thickness(0, 8, 0, 0);
            spOpen.Children.Add(hintOpen);

            Grid rowRoot = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            rowRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            rowRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
            txtRoot = Theme.MakeTextBox(DEFAULT_YYS_ROOT, 0, true);
            txtRoot.Width = double.NaN;
            txtRoot.HorizontalAlignment = HorizontalAlignment.Stretch;
            txtRoot.TextAlignment = TextAlignment.Left;
            txtRoot.ToolTip = "阴阳师安装根目录（含 bin\\onmyoji.exe）";
            Grid.SetColumn(txtRoot, 0);
            rowRoot.Children.Add(txtRoot);
            btnBrowse = Theme.MakeButton("浏览…", () => BtnBrowse_Click());
            Grid.SetColumn(btnBrowse, 2);
            rowRoot.Children.Add(btnBrowse);
            spOpen.Children.Add(rowRoot);

            TextBlock lbRoot = Theme.Label("游戏工作目录（已保存，下次启动自动加载）", 11);
            lbRoot.Margin = new Thickness(0, 6, 0, 0);
            spOpen.Children.Add(lbRoot);

            Border cardOpen = Theme.Card(spOpen);
            Grid.SetRow(cardOpen, 0);
            page.Children.Add(cardOpen);

            // 卡片2：运行状态 + 一键关闭
            StackPanel spRun = new StackPanel();
            spRun.Children.Add(Theme.CardHeader("运行状态", "当前已启动的阴阳师实例统计"));

            Grid rowRun = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            rowRun.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowRun.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            rowRun.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border runDot = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = Theme.BrushFrozen(Theme.Success),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(runDot, 0);
            rowRun.Children.Add(runDot);
            TextBlock lbRun = Theme.Label("运行实例数量（刷新自动更新）", 12.5, true, Theme.BrushFrozen(Theme.TextMain));
            Grid.SetColumn(lbRun, 2);
            rowRun.Children.Add(lbRun);
            spRun.Children.Add(rowRun);

            btnCloseAll = Theme.MakeButton("一键关闭所有游戏窗口", () => { var _ = BtnCloseAll_Click(); }, false, true);
            btnCloseAll.Margin = new Thickness(0, 14, 0, 0);
            spRun.Children.Add(btnCloseAll);

            Border cardRun = Theme.Card(spRun);
            Grid.SetRow(cardRun, 2);
            page.Children.Add(cardRun);

            return page;
        }

        // ---------- 页面：窗口排列 ----------
        private Grid BuildPageArrange()
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel spArr = new StackPanel();
            spArr.Children.Add(Theme.CardHeader("窗口排列", "探测-定位两阶段，自动按网格排布游戏窗口"));

            Grid rowArr = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            rowArr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            TextBlock lbCols = Theme.Label("每行窗口数：", 12.5, true, Theme.BrushFrozen(Theme.TextMain));
            Grid.SetColumn(lbCols, 0);
            rowArr.Children.Add(lbCols);
            txtColsNum = Theme.MakeTextBox("2", 72);
            Grid.SetColumn(txtColsNum, 1);
            rowArr.Children.Add(txtColsNum);
            btnArrange = Theme.MakeButton("排列窗口", () => BtnArrange_Click(), true);
            Grid.SetColumn(btnArrange, 3);
            rowArr.Children.Add(btnArrange);
            btnRefresh = Theme.MakeButton("刷新", () => BtnRefresh_Click());
            Grid.SetColumn(btnRefresh, 5);
            rowArr.Children.Add(btnRefresh);
            spArr.Children.Add(rowArr);

            Grid listHead = new Grid { Margin = new Thickness(0, 16, 0, 6) };
            listHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            listHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock lbListTitle = Theme.Label("识别到的阴阳师窗口", 13, true, Theme.BrushFrozen(Theme.TextMain));
            Grid.SetColumn(lbListTitle, 0);
            listHead.Children.Add(lbListTitle);
            lblWinCount = Theme.Label("0 个", 11.5, true, Theme.BrushFrozen(Theme.AccentLight));
            Grid.SetColumn(lblWinCount, 1);
            listHead.Children.Add(lblWinCount);
            spArr.Children.Add(listHead);

            listWins = new ListView
            {
                Background = Theme.BrushFrozen(Theme.BgInput),
                BorderThickness = new Thickness(0),
                Foreground = Theme.BrushFrozen(Theme.TextMain),
                FontFamily = Theme.FontYH,
                FontSize = 12,
                Padding = new Thickness(2)
            };
            listWins.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            listWins.ItemContainerStyle = ListViewItemStyle();
            listWins.Height = 250;
            spArr.Children.Add(listWins);

            Border cardArr = Theme.Card(spArr);
            Grid.SetRow(cardArr, 0);
            page.Children.Add(cardArr);
            return page;
        }

        // ---------- 页面：声音控制 ----------
        private Grid BuildPageSound()
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel spSound = new StackPanel();
            spSound.Children.Add(Theme.CardHeader("声音跟随聚焦", "聚焦窗口正常音量，失焦窗口自动调小；全部失焦则全部调小"));

            Grid rowSound = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            rowSound.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowSound.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            rowSound.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            swSound = new Theme.SwitchControl(true);
            swSound.Changed += (on) => ChkSound_Changed(on);
            Grid.SetColumn(swSound, 0);
            rowSound.Children.Add(swSound);
            StackPanel spSoundText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            spSoundText.Children.Add(Theme.Label("开启声音跟随聚焦", 13, true, Theme.BrushFrozen(Theme.TextMain)));
            spSoundText.Children.Add(Theme.Label("后台每 500ms 自动检测前台窗口，仅对阴阳师进程生效", 11));
            Grid.SetColumn(spSoundText, 2);
            rowSound.Children.Add(spSoundText);
            spSound.Children.Add(rowSound);

            Border cardSound = Theme.Card(spSound);
            Grid.SetRow(cardSound, 0);
            page.Children.Add(cardSound);

            StackPanel spInfo = new StackPanel();
            spInfo.Children.Add(Theme.CardHeader("工作原理", "为什么需要这个功能"));
            TextBlock info1 = Theme.Label("多开阴阳师时，未聚焦的实例会持续播放声音，影响专注。", 11.5);
            info1.Margin = new Thickness(0, 2, 0, 0);
            spInfo.Children.Add(info1);
            TextBlock info2 = Theme.Label("本功能利用 Windows 音频会话 API（WASAPI）逐实例调节音量，不干扰游戏本体。", 11.5);
            info2.Margin = new Thickness(0, 6, 0, 0);
            spInfo.Children.Add(info2);
            Border cardInfo = Theme.Card(spInfo);
            Grid.SetRow(cardInfo, 2);
            page.Children.Add(cardInfo);

            return page;
        }

        // ---------- 页面：关于 ----------
        private Grid BuildPageAbout()
        {
            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel spAbout = new StackPanel();
            spAbout.Children.Add(Theme.CardHeader("关于本工具", "阴阳师桌面多开辅助"));

            TextBlock ver = Theme.Label("版本 v2.0（WPF 现代化界面）", 13, true, Theme.BrushFrozen(Theme.TextMain));
            ver.Margin = new Thickness(0, 4, 0, 0);
            spAbout.Children.Add(ver);
            TextBlock d1 = Theme.Label("· 多开管理：一键补足目标实例数，支持 1-10 开", 11.5);
            d1.Margin = new Thickness(0, 10, 0, 0);
            spAbout.Children.Add(d1);
            TextBlock d2 = Theme.Label("· 窗口排列：探测-定位两阶段，自动按网格排布", 11.5);
            d2.Margin = new Thickness(0, 6, 0, 0);
            spAbout.Children.Add(d2);
            TextBlock d3 = Theme.Label("· 声音跟随聚焦：WASAPI 逐实例音量控制", 11.5);
            d3.Margin = new Thickness(0, 6, 0, 0);
            spAbout.Children.Add(d3);
            Border cardAbout = Theme.Card(spAbout);
            Grid.SetRow(cardAbout, 0);
            page.Children.Add(cardAbout);

            StackPanel spTip = new StackPanel();
            spTip.Children.Add(Theme.CardHeader("使用提示", "常见问题"));
            TextBlock t1 = Theme.Label("如果操作无反应，请确认窗口未被遮挡；以管理员权限运行可稳定移动游戏窗口。", 11.5);
            t1.Margin = new Thickness(0, 2, 0, 0);
            spTip.Children.Add(t1);
            TextBlock t2 = Theme.Label("首次使用请在「多开管理」中选择正确的阴阳师安装根目录（含 bin\\onmyoji.exe）。", 11.5);
            t2.Margin = new Thickness(0, 6, 0, 0);
            spTip.Children.Add(t2);
            Border cardTip = Theme.Card(spTip);
            Grid.SetRow(cardTip, 2);
            page.Children.Add(cardTip);

            return page;
        }

        // ---------- 日志卡片 ----------
        private Border BuildLogCard()
        {
            StackPanel spLog = new StackPanel();
            Button btnClearLog = Theme.MakeButton("清空", () => txtLog.Clear(), false);
            btnClearLog.Width = 64;
            btnClearLog.HorizontalAlignment = HorizontalAlignment.Right;
            spLog.Children.Add(Theme.CardHeader("运行日志", "操作过程与结果都会记录在这里", btnClearLog));

            txtLog = new TextBox
            {
                Background = Theme.BrushFrozen(Theme.BgInput),
                Foreground = Theme.BrushFrozen(Theme.TextMain),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 8, 10, 8),
                Height = 126
            };
            txtLog.Template = LogTextBoxTemplate();
            spLog.Children.Add(txtLog);
            return Theme.Card(spLog);
        }

        private Theme.SwitchControl swSound;

        private Button TitleButton(string glyph, bool danger)
        {
            Button b = new Button
            {
                Width = 46,
                Height = 34,
                Content = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Foreground = Theme.BrushFrozen(Theme.TextMain),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Focusable = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            ControlTemplate t = new ControlTemplate(typeof(Button));
            t.VisualTree = bd;
            Trigger hov = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(Button.BackgroundProperty,
                Theme.BrushFrozen(danger ? Color.FromRgb(0xC0, 0x3A, 0x3F) : Color.FromRgb(0x26, 0x2E, 0x3B))));
            t.Triggers.Add(hov);
            b.Template = t;
            return b;
        }

        // 置顶按钮模板（带边框 + hover 高亮）
        private ControlTemplate TitleButtonTextTemplate()
        {
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bd.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            bd.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            ControlTemplate t = new ControlTemplate(typeof(Button));
            t.VisualTree = bd;
            Trigger hov = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(Button.BackgroundProperty, Theme.BrushFrozen(Theme.BgHover)));
            hov.Setters.Add(new Setter(Button.BorderBrushProperty, Theme.BrushFrozen(Theme.Accent)));
            hov.Setters.Add(new Setter(Button.ForegroundProperty, Theme.BrushFrozen(Theme.TextMain)));
            t.Triggers.Add(hov);
            return t;
        }

        // 置顶开关：切换窗口 Topmost，并用霓虹色高亮激活态
        private void TogglePin()
        {
            Topmost = !Topmost;
            if (Topmost)
            {
                btnPin.Content = "\uE841"; // PinFill 实心图钉
                btnPin.Background = Theme.AccentGradient();
                btnPin.BorderBrush = Theme.BrushFrozen(Theme.Accent);
                btnPin.Foreground = Theme.BrushFrozen(Colors.White);
            }
            else
            {
                btnPin.Content = "\uE840"; // Pin 轮廓图钉
                btnPin.Background = Brushes.Transparent;
                btnPin.BorderBrush = Theme.BrushFrozen(Theme.BorderLine);
                btnPin.Foreground = Theme.BrushFrozen(Theme.TextSub);
            }
            Log("窗口置顶已" + (Topmost ? "开启" : "关闭") + "（避免被其它窗口遮挡导致点击无效）");
        }

        private static Style ListViewItemStyle()
        {
            Style st = new Style(typeof(ListViewItem));
            FrameworkElementFactory bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(ListViewItem.BackgroundProperty));
            bd.SetValue(Border.PaddingProperty, new Thickness(10, 5, 10, 5));
            bd.SetValue(Border.MarginProperty, new Thickness(0, 1, 0, 1));
            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            bd.AppendChild(cp);
            ControlTemplate t = new ControlTemplate(typeof(ListViewItem));
            t.VisualTree = bd;
            Trigger hov = new Trigger { Property = ListViewItem.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(ListViewItem.BackgroundProperty, Theme.BrushFrozen(Theme.BgHover)));
            t.Triggers.Add(hov);
            Trigger sel = new Trigger { Property = ListViewItem.IsSelectedProperty, Value = true };
            sel.Setters.Add(new Setter(ListViewItem.BackgroundProperty, Theme.BrushFrozen(Theme.BgSel)));
            t.Triggers.Add(sel);
            st.Setters.Add(new Setter(Control.TemplateProperty, t));
            st.Setters.Add(new Setter(Control.ForegroundProperty, Theme.BrushFrozen(Theme.TextMain)));
            st.Setters.Add(new Setter(ListViewItem.BackgroundProperty, Brushes.Transparent));
            st.Setters.Add(new Setter(ListViewItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            st.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(0)));
            return st;
        }

        private static ControlTemplate LogTextBoxTemplate()
        {
            // 与 TextBoxTemplate 同理：PART_ContentHost 必须用 x:Name 注册进 NameScope，
            // FrameworkElementFactory 设名称会导致日志框内容不显示。
            const string xaml =
@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                   xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                   TargetType='TextBox'>
    <Border CornerRadius='8' Background='#0E0F14' BorderBrush='#22242E' BorderThickness='1'>
        <ScrollViewer x:Name='PART_ContentHost'/>
    </Border>
</ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(xaml);
        }

        // ---------- 标题栏交互 ----------
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 点击标题栏上的按钮/开关等交互控件时不触发拖拽（否则 DragMove 会吞掉按钮 Click）
            DependencyObject src = e.OriginalSource as DependencyObject;
            while (src != null && !(src is Button) && !(src is Theme.SwitchControl))
                src = VisualTreeHelper.GetParent(src);
            if (src != null) return;
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        // ---------- 日志 ----------
        private void Log(string s)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action<string>(Log), s);
                return;
            }
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + Environment.NewLine);
            txtLog.ScrollToEnd();
        }

        // ==================== 声音跟随聚焦 ====================
        private void ChkSound_Changed(bool on)
        {
            if (on)
            {
                timerSound.Start();
                Log("声音跟随聚焦监控已开启：聚焦窗口正常音量，失焦窗口自动调小。");
            }
            else
            {
                timerSound.Stop();
                Log("声音跟随聚焦监控已关闭。");
            }
        }

        private int _scanning;

        private void TimerSound_Tick(object sender, EventArgs e)
        {
            // 声音扫描较重（WASAPI 枚举 + 日志），必须移出 UI 线程，否则每 500ms 卡住界面导致点击无反应
            if (Interlocked.Exchange(ref _scanning, 1) == 1) return;
            try
            {
                Task.Run(() =>
                {
                    try { _sf.Scan(); }
                    finally { Interlocked.Exchange(ref _scanning, 0); }
                });
            }
            catch { Interlocked.Exchange(ref _scanning, 0); }
        }

        // ==================== 多开 ====================
        private int CountRunning()
        {
            // 兼容 onmyoji / yys 两种进程名（与一键关闭逻辑一致），排除自身进程
            int selfId = Process.GetCurrentProcess().Id;
            HashSet<int> ids = new HashSet<int>();
            try
            {
                foreach (Process p in Process.GetProcessesByName(PROC_NAME))
                    if (p.Id != selfId) ids.Add(p.Id);
                foreach (Process p in Process.GetProcessesByName("yys"))
                    if (p.Id != selfId) ids.Add(p.Id);
            }
            catch { }
            return ids.Count;
        }

        private void UpdateRunningLabel()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(UpdateRunningLabel));
                return;
            }
            lblRunning.Text = "当前运行实例：" + CountRunning();
            lblRunning.Foreground = Theme.BrushFrozen(Theme.Success);
        }

        private async Task BtnOpen_Click()
        {
            Log("=== 开始多开 ===");
            int target;
            if (!ReadNum(txtOpenNum, 1, 10, out target)) return;
            btnOpen.IsEnabled = false;
            txtOpenNum.IsEnabled = false;
            try
            {
                await Task.Run(() => DoMultiOpen(target));
            }
            finally
            {
                btnOpen.IsEnabled = true;
                txtOpenNum.IsEnabled = true;
                UpdateRunningLabel();
            }
        }

        private bool ReadNum(TextBox box, int min, int max, out int val)
        {
            if (int.TryParse(box.Text.Trim(), out val) && val >= min && val <= max) return true;
            Log(string.Format("输入无效：请输入 {0}-{1} 之间的整数（当前输入：{2}）。", min, max, box.Text));
            box.Focus();
            box.SelectAll();
            return false;
        }

        private void DoMultiOpen(int target)
        {
            if (!ValidateRoot()) return;
            Log(string.Format("本次目标开数 = {0}（读自输入框）", target));
            int running = CountRunning();
            int need = Math.Max(0, target - running);
            Log(string.Format("当前已开 {0} 个实例，目标 {1} 开，需新开 {2} 个", running, target, need));
            if (need == 0)
            {
                Log("已满足目标开数，无需再开。");
                return;
            }
            for (int i = 0; i < need; i++)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = Path.Combine(_yysRoot, "bin", "onmyoji.exe"),
                        WorkingDirectory = _yysRoot,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    Log(string.Format("已启动第 {0}/{1} 个新实例...", i + 1, need));
                }
                catch (Exception ex)
                {
                    Log("启动失败：" + ex.Message);
                    break;
                }
                if (i < need - 1) Thread.Sleep(INSTANCE_INTERVAL_MS);
            }
            Thread.Sleep(6000);
            int now = CountRunning();
            Log("启动流程结束，当前 onmyoji 进程数：" + now);
            if (now < target) Log("警告：部分实例可能未常驻（游戏启动耗时较长，可稍后点「刷新」确认）。");
        }

        // ==================== 一键关闭所有游戏窗口 ====================
        private async Task BtnCloseAll_Click()
        {
            Log("=== 一键关闭所有游戏 ===");
            btnCloseAll.IsEnabled = false;
            try
            {
                await Task.Run(() => DoCloseAll());
            }
            finally
            {
                btnCloseAll.IsEnabled = true;
                Thread.Sleep(500);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateRunningLabel();
                    RefreshList();
                }));
            }
        }

        private void DoCloseAll()
        {
            int selfId = Process.GetCurrentProcess().Id;
            HashSet<int> targets = new HashSet<int>();
            try
            {
                foreach (Process p in Process.GetProcessesByName("onmyoji"))
                    if (p.Id != selfId) targets.Add(p.Id);
                foreach (Process p in Process.GetProcessesByName("yys"))
                    if (p.Id != selfId) targets.Add(p.Id);
            }
            catch { }

            if (targets.Count == 0)
            {
                Log("未发现正在运行的阴阳师进程（onmyoji / yys）。");
                return;
            }

            Log("开始关闭 " + targets.Count + " 个阴阳师进程（先优雅关闭，超时 3 秒后强制结束）...");
            foreach (int pid in targets)
            {
                try
                {
                    Process p = Process.GetProcessById(pid);
                    bool closed = p.CloseMainWindow();
                    if (closed)
                    {
                        for (int round = 0; round < 3 && !p.HasExited; round++)
                        {
                            bool clicked = false;
                            for (int i = 0; i < 30 && !p.HasExited; i++)
                            {
                                if (TryConfirmExit((uint)pid)) { clicked = true; break; }
                                Thread.Sleep(100);
                            }
                            if (clicked && !p.HasExited)
                            {
                                if (p.WaitForExit(1500)) break;
                            }
                        }
                    }
                    if (p.WaitForExit(3000))
                    {
                        Log(string.Format("[PID {0}] 已优雅关闭", pid));
                    }
                    else
                    {
                        if (!p.HasExited) p.Kill();
                        Log(string.Format("[PID {0}] 超时未退出，已强制结束", pid));
                    }
                    p.Dispose();
                }
                catch (Exception ex)
                {
                    Log(string.Format("[PID {0}] 关闭失败：{1}", pid, ex.Message));
                }
            }
            Log("全部关闭流程结束。");
        }

        private static bool TryConfirmExit(uint pid)
        {
            IntPtr dlg = IntPtr.Zero;
            Native.EnumWindows((h, l) =>
            {
                uint wpid;
                Native.GetWindowThreadProcessId(h, out wpid);
                if (wpid != pid) return true;
                var cls = new StringBuilder(64);
                Native.GetClassName(h, cls, 64);
                if (cls.ToString() == "#32770")
                {
                    var t = new StringBuilder(256);
                    Native.GetWindowText(h, t, 256);
                    if (t.ToString().Contains("退出游戏")) { dlg = h; return false; }
                }
                return true;
            }, IntPtr.Zero);

            if (dlg == IntPtr.Zero) return false;

            IntPtr okBtn = IntPtr.Zero;
            Native.EnumChildWindows(dlg, (ch, cl) =>
            {
                var c = new StringBuilder(64);
                Native.GetClassName(ch, c, 64);
                if (c.ToString() == "Button")
                {
                    var t = new StringBuilder(256);
                    Native.GetWindowText(ch, t, 256);
                    string bt = t.ToString();
                    if (bt == "确定" || bt == "確定") { okBtn = ch; return false; }
                }
                return true;
            }, IntPtr.Zero);

            if (okBtn == IntPtr.Zero) return false;
            Native.SendMessage(okBtn, Native.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Native.RECT r;
            if (Native.GetWindowRect(okBtn, out r))
            {
                int cx = (r.L + r.R) / 2;
                int cy = (r.T + r.B) / 2;
                Native.SetCursorPos(cx, cy);
                Thread.Sleep(50);
                Native.mouse_event(Native.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                Thread.Sleep(50);
                Native.mouse_event(Native.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            }
            return true;
        }

        // ==================== 窗口识别 ====================
        private static string GetProcessPath(IntPtr hwnd)
        {
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return "";
            try
            {
                StringBuilder sb = new StringBuilder(1024);
                uint size = 1024;
                Native.QueryFullProcessImageName(h, 0, sb, ref size);
                return sb.ToString();
            }
            finally
            {
                Native.CloseHandle(h);
            }
        }

        private static string GetTitle(IntPtr hwnd)
        {
            StringBuilder sb = new StringBuilder(512);
            Native.GetWindowText(hwnd, sb, 512);
            return sb.ToString();
        }

        private static bool IsYysWindow(IntPtr hwnd)
        {
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == (uint)Process.GetCurrentProcess().Id) return false;

            string title = GetTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title)) return false;

            string lowerTitle = title.ToLowerInvariant();
            if (lowerTitle.Contains("mpay") || lowerTitle.Contains("pay") || lowerTitle.Contains("tips") ||
                lowerTitle.Contains("form") || lowerTitle.Contains("login") || lowerTitle.Contains("dialog") ||
                lowerTitle.Contains("popup") || lowerTitle.Contains("notice") || lowerTitle.Contains("menu") ||
                lowerTitle.Contains("setting") || lowerTitle.Contains("dlg") || lowerTitle.Contains("wnd"))
                return false;

            string path = GetProcessPath(hwnd).ToLowerInvariant();
            if (!path.Contains("onmyoji") && !path.Contains("yys") && !path.Contains("阴阳师"))
                return false;

            if (title.Contains("阴阳师") && !title.Contains("排列") && !title.Contains("工具"))
                return true;
            return false;
        }

        private static List<YysWindow> EnumerateYys()
        {
            List<YysWindow> list = new List<YysWindow>();
            Native.EnumWindows(delegate(IntPtr hwnd, IntPtr lp)
            {
                if (Native.IsWindowVisible(hwnd) && IsYysWindow(hwnd))
                {
                    uint pid;
                    Native.GetWindowThreadProcessId(hwnd, out pid);
                    Native.RECT r;
                    Native.GetWindowRect(hwnd, out r);
                    list.Add(new YysWindow { Hwnd = hwnd, Pid = pid, Title = GetTitle(hwnd), Rect = r, Focused = (Native.GetForegroundWindow() == hwnd) });
                }
                return true;
            }, IntPtr.Zero);
            return list;
        }

        private void BtnRefresh_Click()
        {
            UpdateRunningLabel();
            RefreshList();
        }

        private string ConfigPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini"); }
        }

        private void LoadConfig()
        {
            try
            {
                string cfg = ConfigPath;
                if (File.Exists(cfg))
                {
                    string line = File.ReadAllText(cfg, Encoding.UTF8).Trim();
                    if (line.Length > 0 && Directory.Exists(line))
                    {
                        _yysRoot = line;
                    }
                }
            }
            catch { }
            txtRoot.Text = _yysRoot;
        }

        private void SaveConfig()
        {
            try { File.WriteAllText(ConfigPath, _yysRoot, Encoding.UTF8); }
            catch (Exception ex) { Log("保存配置失败：" + ex.Message); }
        }

        private bool ValidateRoot()
        {
            string exe = Path.Combine(_yysRoot, "bin", "onmyoji.exe");
            if (Directory.Exists(_yysRoot) && File.Exists(exe)) return true;
            Log("工作目录无效：" + _yysRoot + "（未找到 bin\\onmyoji.exe，请点「浏览…」重新选择）");
            return false;
        }

        private void BtnBrowse_Click()
        {
            using (System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "选择阴阳师安装根目录（含 bin\\onmyoji.exe 的目录）";
                dlg.SelectedPath = Directory.Exists(_yysRoot) ? _yysRoot : DEFAULT_YYS_ROOT;
                dlg.ShowNewFolderButton = false;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string dir = dlg.SelectedPath;
                    if (!File.Exists(Path.Combine(dir, "bin", "onmyoji.exe")))
                    {
                        Log("所选目录未找到 bin\\onmyoji.exe，请重新选择：" + dir);
                        return;
                    }
                    _yysRoot = dir;
                    txtRoot.Text = dir;
                    SaveConfig();
                    Log("工作目录已设置为：" + dir + "（已保存，下次启动自动加载）");
                }
            }
        }

        private void RefreshList()
        {
            List<YysWindow> wins = EnumerateYys();
            listWins.Items.Clear();
            foreach (YysWindow w in wins)
            {
                string mark = w.Focused ? "（聚焦）" : "";
                listWins.Items.Add(string.Format("[PID {0}]  {1}  {2}", w.Pid, w.Title, mark));
            }
            lblWinCount.Text = wins.Count + " 个";
            Log("识别到阴阳师窗口 " + wins.Count + " 个");
        }

        // ==================== 排列（探测-定位两阶段） ====================
        private void BtnArrange_Click()
        {
            int cols;
            if (!ReadNum(txtColsNum, 1, 4, out cols)) return;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                DoArrange(cols);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void DoArrange(int cols)
        {
            List<YysWindow> wins = EnumerateYys();
            if (wins.Count == 0)
            {
                Log("未识别到任何阴阳师窗口，请先启动游戏后点「刷新」。");
                return;
            }
            Log("开始排列 " + wins.Count + " 个窗口，每行 " + cols + " 个");

            Native.RECT wa = new Native.RECT();
            Native.SystemParametersInfo(Native.SPI_GETWORKAREA, 0, ref wa, 0);
            int workW = wa.R - wa.L;
            int workH = wa.B - wa.T;
            int rows = (wins.Count + cols - 1) / cols;
            int cellW = workW / cols;
            int targetW = cellW - 8;
            int startH = Math.Min(workH / rows, 1000);

            Log("探测目标尺寸（客户端会强制修正窗口尺寸）...");
            int aw, ah;
            ProbeSize(wins[0].Hwnd, targetW, startH, out aw, out ah);
            Log(string.Format("客户端接受尺寸：{0} x {1}", aw, ah));

            int gap = 8;
            for (int i = 0; i < wins.Count; i++)
            {
                YysWindow w = wins[i];
                Native.ShowWindow(w.Hwnd, Native.SW_RESTORE);
                Native.SetWindowPos(w.Hwnd, IntPtr.Zero, 0, 0, aw, ah, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                Thread.Sleep(300);

                Native.RECT rc;
                Native.GetWindowRect(w.Hwnd, out rc);
                int cw = rc.R - rc.L;
                int ch = rc.B - rc.T;

                int row = i / cols;
                int col = i % cols;
                int x = wa.L + col * cellW + (cellW - cw) / 2;
                int y = wa.T + row * (ch + gap);

                Native.SetWindowPos(w.Hwnd, IntPtr.Zero, x, y, cw, ch, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                Log(string.Format("[{0}/{1}] {2} -> ({3}, {4}) {5}x{6}", i + 1, wins.Count, w.Title, x, y, cw, ch));
            }
            Log("排列完成。");
        }

        private void ProbeSize(IntPtr hwnd, int targetW, int startH, out int bestW, out int bestH)
        {
            int h = startH;
            Native.ShowWindow(hwnd, Native.SW_RESTORE);
            while (h >= 300)
            {
                Native.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, targetW, h, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                Thread.Sleep(PROBE_STEP_MS);
                Native.RECT r;
                Native.GetWindowRect(hwnd, out r);
                int w = r.R - r.L;
                if (w <= targetW + 4)
                {
                    bestW = w;
                    bestH = r.B - r.T;
                    return;
                }
                h -= 40;
            }
            Native.RECT r2;
            Native.GetWindowRect(hwnd, out r2);
            bestW = r2.R - r2.L;
            bestH = r2.B - r2.T;
        }

        // ==================== 自检模式（无 UI，验证识别逻辑） ====================
        public static void SelfTestCore()
        {
            string log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.log");
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("self-test at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                List<YysWindow> wins = EnumerateYys();
                sb.AppendLine("found=" + wins.Count);
                foreach (YysWindow w in wins)
                    sb.AppendLine("pid=" + w.Pid + " | " + w.Title);
                File.WriteAllText(log, sb.ToString(), Encoding.UTF8);
                Console.WriteLine("SELFTEST OK found=" + wins.Count);
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(log, "EXCEPTION: " + ex.ToString(), Encoding.UTF8); } catch { }
                Console.WriteLine("SELFTEST FAIL: " + ex.Message);
            }
        }

        // ==================== 入口 ====================
        [STAThread]
        public static void Main(string[] args)
        {
            try { Native.SetProcessDpiAwareness(2); } catch { }

            if (args.Length > 0 && args[0] == "--selftest")
            {
                SelfTestCore();
                return;
            }

            bool debug = args.Length > 0 && args[0] == "--debugui";
            try
            {
                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                MainWindow win = new MainWindow();
                win.SourceInitialized += (s, e) =>
                {
                    win.UpdateRunningLabel();
                    win.RefreshList();
                };
                win.Show();
                app.Run(win);
            }
            catch (Exception ex)
            {
                if (debug)
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log"),
                            ex.ToString(), Encoding.UTF8);
                    }
                    catch { }
                    return;
                }
                throw;
            }
        }
    }
}

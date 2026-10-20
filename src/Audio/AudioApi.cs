using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Audio
{
    /// <summary>枚举系统音频会话（提供按 PID 调整音量的能力）</summary>
    internal static class AudioApi
    {
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
                if (hr == 0) DumpDeviceSessions(device, result);

                IMMDeviceCollection coll;
                hr = enumerator.EnumAudioEndpoints(0, 0x0F, out coll);
                if (hr == 0)
                {
                    uint n;
                    if (coll.GetCount(out n) == 0)
                    {
                        for (uint i = 0; i < n; i++)
                        {
                            IMMDevice dev;
                            if (coll.Item(i, out dev) != 0) continue;
                            DumpDeviceSessions(dev, result);
                        }
                    }
                }
            }
            catch (Exception) { }
            return result;
        }

        private static void DumpDeviceSessions(IMMDevice device, List<SessionInfo> result)
        {
            try
            {
                IntPtr mgrPtr;
                Guid mgrIid = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
                int hr = device.Activate(ref mgrIid, 7, IntPtr.Zero, out mgrPtr);
                if (hr != 0) return;

                IAudioSessionManager2 mgr = (IAudioSessionManager2)Marshal.GetObjectForIUnknown(mgrPtr);
                IAudioSessionEnumerator sessions;
                hr = mgr.GetSessionEnumerator(out sessions);
                if (hr != 0) return;

                int count;
                if (sessions.GetCount(out count) != 0) return;
                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl ctl;
                    if (sessions.GetSession(i, out ctl) != 0) continue;
                    IAudioSessionControl2 ctl2 = (IAudioSessionControl2)ctl;
                    uint pid;
                    if (ctl2.GetProcessId(out pid) != 0) continue;
                    ISimpleAudioVolume vol = (ISimpleAudioVolume)ctl;
                    string name = "";
                    try { if (pid != 0) name = Process.GetProcessById((int)pid).ProcessName; } catch { }
                    result.Add(new SessionInfo { Pid = pid, ProcessName = name, Volume = vol });
                }
            }
            catch (Exception) { }
        }

        public static float GetCurrentVolume(ISimpleAudioVolume vol)
        {
            float v = 1.0f;
            try { vol.GetMasterVolume(out v); } catch { }
            return v;
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
}

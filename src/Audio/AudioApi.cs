using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Audio
{
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
}

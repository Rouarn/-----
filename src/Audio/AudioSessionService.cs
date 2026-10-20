using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Audio
{
    /// <summary>音频会话音量操作的抽象，便于在没有音频设备的环境中测试编排逻辑。</summary>
    internal interface IAudioSessionService
    {
        HashSet<int> ApplyVolumes(IDictionary<int, float> desiredVolumes);
        Dictionary<int, float> GetVolumes(IEnumerable<int> processIds);
    }

    /// <summary>
    /// 按 PID 调整音频会话音量。每次扫描枚举所有活动渲染设备，
    /// 并在 finally 中释放每个 COM 对象（旧实现每 500ms 泄漏一批 RCW）。
    /// </summary>
    internal sealed class AudioSessionService : IAudioSessionService
    {
        private readonly object _gate = new object();

        /// <summary>把指定 PID 的所有音频会话设置为目标音量，返回至少成功设置过一个会话的 PID。</summary>
        public HashSet<int> ApplyVolumes(IDictionary<int, float> desiredVolumes)
        {
            HashSet<int> applied = new HashSet<int>();
            if (desiredVolumes == null || desiredVolumes.Count == 0) return applied;

            lock (_gate)
            {
                IMMDeviceEnumerator enumerator = null;
                IMMDeviceCollection devices = null;
                try
                {
                    enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                    int hr = enumerator.EnumAudioEndpoints(AudioConstants.Render, AudioConstants.ActiveDevices, out devices);
                    if (hr != 0 || devices == null) return applied;

                    uint count;
                    if (devices.GetCount(out count) != 0) return applied;

                    for (uint i = 0; i < count; i++)
                    {
                        IMMDevice device = null;
                        try
                        {
                            if (devices.Item(i, out device) != 0 || device == null) continue;
                            ApplyForDevice(device, desiredVolumes, applied);
                        }
                        catch { }
                        finally { Release(device); }
                    }
                }
                catch { }
                finally
                {
                    Release(devices);
                    Release(enumerator);
                }
            }

            return applied;
        }

        /// <summary>读取指定 PID 当前的主音量（每个 PID 取第一个会话），用于保存原始音量。</summary>
        public Dictionary<int, float> GetVolumes(IEnumerable<int> processIds)
        {
            Dictionary<int, float> result = new Dictionary<int, float>();
            if (processIds == null) return result;

            HashSet<int> wanted = new HashSet<int>();
            foreach (int processId in processIds)
            {
                if (processId > 0) wanted.Add(processId);
            }
            if (wanted.Count == 0) return result;

            lock (_gate)
            {
                IMMDeviceEnumerator enumerator = null;
                IMMDeviceCollection devices = null;
                try
                {
                    enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                    int hr = enumerator.EnumAudioEndpoints(AudioConstants.Render, AudioConstants.ActiveDevices, out devices);
                    if (hr != 0 || devices == null) return result;

                    uint count;
                    if (devices.GetCount(out count) != 0) return result;

                    for (uint i = 0; i < count; i++)
                    {
                        IMMDevice device = null;
                        try
                        {
                            if (devices.Item(i, out device) != 0 || device == null) continue;
                            ReadForDevice(device, wanted, result);
                        }
                        catch { }
                        finally { Release(device); }
                    }
                }
                catch { }
                finally
                {
                    Release(devices);
                    Release(enumerator);
                }
            }

            return result;
        }

        private static void ReadForDevice(IMMDevice device, HashSet<int> wanted, Dictionary<int, float> result)
        {
            IntPtr managerPointer = IntPtr.Zero;
            IAudioSessionManager2 manager = null;
            IAudioSessionEnumerator sessions = null;
            bool managerPointerReleased = false;

            try
            {
                Guid managerIid = typeof(IAudioSessionManager2).GUID;
                int hr = device.Activate(ref managerIid, AudioConstants.ClsCtxAll, IntPtr.Zero, out managerPointer);
                if (hr != 0 || managerPointer == IntPtr.Zero) return;

                manager = (IAudioSessionManager2)Marshal.GetObjectForIUnknown(managerPointer);
                Marshal.Release(managerPointer);
                managerPointerReleased = true;
                managerPointer = IntPtr.Zero;
                if (manager == null) return;

                if (manager.GetSessionEnumerator(out sessions) != 0 || sessions == null) return;

                int sessionCount;
                if (sessions.GetCount(out sessionCount) != 0) return;

                for (int i = 0; i < sessionCount; i++)
                {
                    IAudioSessionControl control = null;
                    IAudioSessionControl2 control2 = null;
                    ISimpleAudioVolume volume = null;
                    try
                    {
                        if (sessions.GetSession(i, out control) != 0 || control == null) continue;

                        control2 = control as IAudioSessionControl2;
                        if (control2 == null) continue;
                        if (control2.IsSystemSoundsSession() == 0) continue;

                        uint rawPid;
                        if (control2.GetProcessId(out rawPid) != 0) continue;
                        int processId = unchecked((int)rawPid);
                        if (!wanted.Contains(processId) || result.ContainsKey(processId)) continue;

                        volume = control as ISimpleAudioVolume;
                        if (volume == null) continue;

                        float level;
                        if (volume.GetMasterVolume(out level) == 0)
                            result[processId] = level < 0f ? 0f : (level > 1f ? 1f : level);
                    }
                    catch { }
                    finally
                    {
                        ReleaseDistinct(control, control2, volume);
                    }
                }
            }
            catch { }
            finally
            {
                Release(sessions);
                Release(manager);
                if (!managerPointerReleased && managerPointer != IntPtr.Zero)
                {
                    try { Marshal.Release(managerPointer); }
                    catch { }
                }
            }
        }

        private static void ApplyForDevice(IMMDevice device, IDictionary<int, float> desiredVolumes, HashSet<int> applied)
        {
            IntPtr managerPointer = IntPtr.Zero;
            IAudioSessionManager2 manager = null;
            IAudioSessionEnumerator sessions = null;
            bool managerPointerReleased = false;

            try
            {
                Guid managerIid = typeof(IAudioSessionManager2).GUID;
                int hr = device.Activate(ref managerIid, AudioConstants.ClsCtxAll, IntPtr.Zero, out managerPointer);
                if (hr != 0 || managerPointer == IntPtr.Zero) return;

                manager = (IAudioSessionManager2)Marshal.GetObjectForIUnknown(managerPointer);
                Marshal.Release(managerPointer);
                managerPointerReleased = true;
                managerPointer = IntPtr.Zero;
                if (manager == null) return;

                if (manager.GetSessionEnumerator(out sessions) != 0 || sessions == null) return;

                int sessionCount;
                if (sessions.GetCount(out sessionCount) != 0) return;

                for (int i = 0; i < sessionCount; i++)
                {
                    IAudioSessionControl control = null;
                    IAudioSessionControl2 control2 = null;
                    ISimpleAudioVolume volume = null;
                    try
                    {
                        if (sessions.GetSession(i, out control) != 0 || control == null) continue;

                        control2 = control as IAudioSessionControl2;
                        if (control2 == null) continue;

                        // IsSystemSoundsSession 返回 S_OK 表示这是系统提示音会话
                        if (control2.IsSystemSoundsSession() == 0) continue;

                        uint rawPid;
                        if (control2.GetProcessId(out rawPid) != 0) continue;
                        int processId = unchecked((int)rawPid);

                        float level;
                        if (!desiredVolumes.TryGetValue(processId, out level)) continue;

                        volume = control as ISimpleAudioVolume;
                        if (volume == null) continue;

                        Guid eventContext = Guid.Empty;
                        if (volume.SetMasterVolume(level, ref eventContext) == 0) applied.Add(processId);
                    }
                    catch { }
                    finally
                    {
                        // 一次会话最多持有 control/control2/volume 三个 RCW，按引用去重释放
                        ReleaseDistinct(control, control2, volume);
                    }
                }
            }
            catch { }
            finally
            {
                Release(sessions);
                Release(manager);
                if (!managerPointerReleased && managerPointer != IntPtr.Zero)
                {
                    try { Marshal.Release(managerPointer); }
                    catch { }
                }
            }
        }

        private static void ReleaseDistinct(params object[] objects)
        {
            HashSet<object> released = new HashSet<object>(ReferenceComparer.Instance);
            for (int i = objects.Length - 1; i >= 0; i--)
            {
                object value = objects[i];
                if (value == null || released.Contains(value)) continue;
                released.Add(value);
                Release(value);
            }
        }

        private static void Release(object value)
        {
            if (value == null) return;
            try
            {
                if (Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
            }
            catch { }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object x, object y)
            {
                return object.ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}

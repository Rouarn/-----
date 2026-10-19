using System;
using System.Collections.Generic;
using OnmyojiTools.Core;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Audio
{
    /// <summary>声音跟随聚焦控制器：前台聚焦窗口恢复为系统总音量响度，失焦窗口声音调小；全部失焦则全部调小</summary>
    internal class SoundFocusController
    {
        private const float LOW_VOLUME = 0.15f;
        private const float FOCUS_VOLUME = 1.0f;
        // 记录每个 pid 上次设置的目标音量（float），用于避免无谓重复设置
        private readonly Dictionary<uint, float> _lastSet = new Dictionary<uint, float>();

        public void Scan()
        {
            HashSet<uint> targets = GameProcesses.CollectPids();
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
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OnmyojiTools.Core
{
    /// <summary>阴阳师进程收集（兼容 onmyoji / yys 两种进程名）</summary>
    internal static class GameProcesses
    {
        public const string MainName = "onmyoji";
        public const string AltName = "yys";

        /// <summary>收集正在运行的游戏进程 Id（排除工具自身进程）</summary>
        public static HashSet<int> CollectIds()
        {
            int selfId = Process.GetCurrentProcess().Id;
            HashSet<int> ids = new HashSet<int>();
            try
            {
                foreach (Process p in Process.GetProcessesByName(MainName))
                    if (p.Id != selfId) ids.Add(p.Id);
                foreach (Process p in Process.GetProcessesByName(AltName))
                    if (p.Id != selfId) ids.Add(p.Id);
            }
            catch { }
            return ids;
        }

        /// <summary>收集正在运行的游戏进程 Pid（不排除自身，用于音频会话按 PID 匹配）</summary>
        public static HashSet<uint> CollectPids()
        {
            HashSet<uint> set = new HashSet<uint>();
            try
            {
                foreach (Process p in Process.GetProcessesByName(MainName)) set.Add((uint)p.Id);
                foreach (Process p in Process.GetProcessesByName(AltName)) set.Add((uint)p.Id);
            }
            catch { }
            return set;
        }
    }
}

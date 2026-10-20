using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OnmyojiTools.Core
{
    internal sealed class ProcessResourceInfo
    {
        public int ProcessId { get; set; }
        public string Title { get; set; }
        public long WorkingSetBytes { get; set; }
        public TimeSpan CpuTime { get; set; }
        public DateTime? StartTime { get; set; }
        public bool Available { get; set; }
    }

    /// <summary>按需读取游戏进程的内存、CPU 累计时间与运行时长；不启动后台采样。</summary>
    internal static class ProcessResourceReader
    {
        public static IReadOnlyList<ProcessResourceInfo> Read(IReadOnlyList<GameWindowInfo> windows)
        {
            List<ProcessResourceInfo> result = new List<ProcessResourceInfo>();
            if (windows == null) return result;

            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < windows.Count; i++)
            {
                int processId = windows[i].ProcessId;
                if (processId <= 0 || !seen.Add(processId)) continue;

                ProcessResourceInfo info = new ProcessResourceInfo
                {
                    ProcessId = processId,
                    Title = windows[i].Title,
                    Available = false
                };

                Process process = null;
                try
                {
                    process = Process.GetProcessById(processId);
                    if (!process.HasExited)
                    {
                        info.Available = true;
                        info.WorkingSetBytes = process.WorkingSet64;
                        info.CpuTime = process.TotalProcessorTime;
                        try { info.StartTime = process.StartTime; } catch { }
                    }
                }
                catch { }
                finally
                {
                    if (process != null) process.Dispose();
                }

                result.Add(info);
            }
            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>当前版本游戏进程枚举；所有 Process 句柄都会及时释放。</summary>
    internal sealed class GameProcessService : IGameProcessService
    {
        private const string ProcessName = "onmyoji";
        private readonly int _selfProcessId;

        public GameProcessService()
        {
            using (Process self = Process.GetCurrentProcess()) _selfProcessId = self.Id;
        }

        public HashSet<int> GetRunningProcessIds()
        {
            HashSet<int> ids = new HashSet<int>();
            Process[] processes;
            try { processes = Process.GetProcessesByName(ProcessName); }
            catch { return ids; }

            for (int i = 0; i < processes.Length; i++)
            {
                Process process = processes[i];
                try
                {
                    if (process.Id != _selfProcessId) ids.Add(process.Id);
                }
                catch { }
                finally { process.Dispose(); }
            }
            return ids;
        }

        public IReadOnlyList<GameProcessInfo> GetRunningGames()
        {
            List<GameProcessInfo> games = new List<GameProcessInfo>();
            Process[] processes;
            try { processes = Process.GetProcessesByName(ProcessName); }
            catch { return games; }

            for (int i = 0; i < processes.Length; i++)
            {
                Process process = processes[i];
                try
                {
                    int id = process.Id;
                    if (id == _selfProcessId) continue;

                    string path = ProcessPathReader.TryGetExecutablePath(id);
                    if (!string.IsNullOrEmpty(path) && !GameWindowMatcher.IsGameExecutable(path)) continue;

                    DateTime? start = null;
                    try { start = process.StartTime; } catch { }

                    games.Add(new GameProcessInfo
                    {
                        ProcessId = id,
                        ProcessName = SafeProcessName(process),
                        ExecutablePath = path,
                        StartTime = start
                    });
                }
                catch { }
                finally { process.Dispose(); }
            }

            return games.OrderBy(g => g.ProcessId).ToList();
        }

        public int CountRunningGames()
        {
            return GetRunningProcessIds().Count;
        }

        private static string SafeProcessName(Process process)
        {
            try { return process.ProcessName; }
            catch { return ""; }
        }
    }
}

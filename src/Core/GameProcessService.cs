using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>游戏进程枚举；所有 Process 句柄都会及时释放。</summary>
    internal sealed class GameProcessService : IGameProcessService
    {
        private static readonly string[] ProcessNames = { "onmyoji", "yys" };
        private readonly int _selfProcessId;

        public GameProcessService()
        {
            using (Process self = Process.GetCurrentProcess()) _selfProcessId = self.Id;
        }

        public HashSet<int> GetRunningProcessIds()
        {
            HashSet<int> ids = new HashSet<int>();
            for (int i = 0; i < ProcessNames.Length; i++)
            {
                Process[] processes = null;
                try { processes = Process.GetProcessesByName(ProcessNames[i]); }
                catch { continue; }

                for (int j = 0; j < processes.Length; j++)
                {
                    Process process = processes[j];
                    try
                    {
                        if (process.Id != _selfProcessId) ids.Add(process.Id);
                    }
                    catch { }
                    finally { process.Dispose(); }
                }
            }
            return ids;
        }

        public IReadOnlyList<GameProcessInfo> GetRunningGames()
        {
            List<GameProcessInfo> games = new List<GameProcessInfo>();
            HashSet<int> seen = new HashSet<int>();

            for (int i = 0; i < ProcessNames.Length; i++)
            {
                Process[] processes = null;
                try { processes = Process.GetProcessesByName(ProcessNames[i]); }
                catch { continue; }

                for (int j = 0; j < processes.Length; j++)
                {
                    Process process = processes[j];
                    try
                    {
                        int id = process.Id;
                        if (id == _selfProcessId || !seen.Add(id)) continue;

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

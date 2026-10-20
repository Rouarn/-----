using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace OnmyojiTools.App
{
    /// <summary>
    /// 下载更新并生成一个等待当前进程退出后替换 exe 的批处理。
    /// 只有用户明确确认才会调用；替换前保留 .bak，不静默执行。
    /// </summary>
    internal static class UpdateInstaller
    {
        public static bool Download(string url, out string file, out string error)
        {
            file = null;
            error = null;
            try
            {
                string target = Path.Combine(Path.GetTempPath(),
                    "OnmyojiTools-update-" + Guid.NewGuid().ToString("N") + ".exe");
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "OnmyojiTools/" + UpdateChecker.CurrentVersion);
                    client.DownloadFile(url, target);
                }

                FileInfo info = new FileInfo(target);
                if (!info.Exists || info.Length < 100000)
                {
                    try { File.Delete(target); } catch { }
                    error = "下载的更新文件不完整。";
                    return false;
                }
                file = target;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool ScheduleReplace(string downloadedFile, out string error)
        {
            error = null;
            string batch = null;
            try
            {
                if (!File.Exists(downloadedFile))
                {
                    error = "更新文件不存在。";
                    return false;
                }

                string current = Process.GetCurrentProcess().MainModule.FileName;
                string backup = current + ".bak";
                batch = Path.Combine(Path.GetTempPath(),
                    "OnmyojiTools-apply-" + Guid.NewGuid().ToString("N") + ".bat");

                StringBuilder script = new StringBuilder();
                script.AppendLine("@echo off");
                script.AppendLine("timeout /t 2 /nobreak >nul");
                script.AppendLine("copy /y \"" + current + "\" \"" + backup + "\" >nul");
                script.AppendLine("copy /y \"" + downloadedFile + "\" \"" + current + "\" >nul");
                script.AppendLine("start \"\" \"" + current + "\"");
                script.AppendLine("del \"%~f0\"");
                File.WriteAllText(batch, script.ToString(), new UTF8Encoding(false));

                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c \"" + batch + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(info);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                if (batch != null)
                {
                    try { File.Delete(batch); } catch { }
                }
                return false;
            }
        }
    }
}

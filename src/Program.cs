using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using OnmyojiTools.Core;
using OnmyojiTools.Interop;
using OnmyojiTools.Ui;

namespace OnmyojiTools
{
    /// <summary>程序入口：命令行模式（--selftest / --debugui）与 WPF 主程序</summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try { Native.SetProcessDpiAwareness(2); } catch { }

            if (args.Length > 0 && args[0] == "--selftest")
            {
                SelfTest();
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

        // ==================== 自检模式（无 UI，验证识别逻辑） ====================
        private static void SelfTest()
        {
            string log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.log");
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("self-test at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                List<YysWindow> wins = WindowFinder.EnumerateYys();
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
    }
}

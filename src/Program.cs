using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using OnmyojiTools.App;
using OnmyojiTools.Diagnostics;
using OnmyojiTools.Interop;
using OnmyojiTools.Ui;

namespace OnmyojiTools
{
    /// <summary>程序入口：自检模式与 WPF 主程序；集中处理未捕获异常。</summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try { NativeMethods.SetProcessDpiAwareness(2); }
            catch { }

            if (args != null && args.Length > 0 && string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = SelfTest.Run();
                return;
            }

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            AppLogger logger = new AppLogger();
            string crashLog = Path.Combine(baseDirectory, "crash.log");

            Application application = new Application();
            application.ShutdownMode = ShutdownMode.OnMainWindowClose;

            application.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
            {
                logger.LogError("界面线程未处理异常", e.Exception);
                TryWriteCrashLog(crashLog, e.Exception);
                MessageBox.Show(
                    "程序遇到未处理的异常，即将退出。\n\n" + e.Exception.Message,
                    "阴阳师工具", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
                application.Shutdown(1);
            };

            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Exception exception = e.ExceptionObject as Exception;
                if (exception != null)
                {
                    logger.LogError("后台线程未处理异常", exception);
                    TryWriteCrashLog(crashLog, exception);
                }
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += delegate(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
            {
                logger.LogError("后台任务未观察异常", e.Exception);
                e.SetObserved();
            };

            try
            {
                SettingsStore store = new SettingsStore(Path.Combine(baseDirectory, "config.ini"));
                AppSettings settings = store.Load();
                AppController controller = AppController.CreateDefault(settings, store, logger);
                MainWindow window = new MainWindow(controller, logger);
                application.Run(window);
            }
            catch (Exception ex)
            {
                logger.LogError("启动失败", ex);
                TryWriteCrashLog(crashLog, ex);
                MessageBox.Show(
                    "程序启动失败：\n\n" + ex.Message,
                    "阴阳师工具", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.ExitCode = 1;
            }
        }

        private static void TryWriteCrashLog(string path, Exception exception)
        {
            try
            {
                File.WriteAllText(path,
                    "阴阳师工具崩溃日志 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                    (exception == null ? "" : exception.ToString()));
            }
            catch { }
        }
    }
}

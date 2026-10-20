using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OnmyojiTools.Audio;
using OnmyojiTools.Core;
using OnmyojiTools.Diagnostics;
using OnmyojiTools.Interop;

namespace OnmyojiTools.App
{
    /// <summary>不启动界面的自检：纯逻辑 + 配置往返 + 当前环境枚举 + 一次 COM 音量枚举。</summary>
    internal static class SelfTest
    {
        public static int Run()
        {
            StringBuilder output = new StringBuilder();
            int passed = 0;
            int failed = 0;

            Action<string, bool> check = delegate(string name, bool ok)
            {
                output.AppendLine((ok ? "PASS  " : "FAIL  ") + name);
                if (ok) passed++; else failed++;
            };

            output.AppendLine("阴阳师工具自检 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            try
            {
                TestInputValidation(check);
                TestGridLayout(check);
                TestWindowMatcher(check);
                TestSettingsStore(check);
                TestLayoutPresets(check);
                TestMonitors(check);
                TestWindowPlacements(check);
                TestInstanceLabels(check);

                GameProcessService processes = new GameProcessService();
                GameWindowService windows = new GameWindowService();
                IReadOnlyList<GameProcessInfo> running = processes.GetRunningGames();
                IReadOnlyList<GameWindowInfo> found = windows.GetGameWindows();
                output.AppendLine("ENV   游戏进程 " + running.Count + " 个，游戏窗口 " + found.Count + " 个。");
                check("当前环境枚举不抛异常", true);

                try
                {
                    AudioSessionService audio = new AudioSessionService();
                    Dictionary<int, float> desired = new Dictionary<int, float>();
                    desired[int.MaxValue] = 0.5f;
                    for (int i = 0; i < 5; i++) audio.ApplyVolumes(desired);
                    check("WASAPI 会话枚举与释放不抛异常", true);
                }
                catch (Exception ex)
                {
                    check("WASAPI 会话枚举与释放不抛异常", false);
                    output.AppendLine("       " + ex.Message);
                }

                RecordingAudioService recording = new RecordingAudioService();
                AppLogger soundLogger = new AppLogger();
                using (SoundFocusService sound = new SoundFocusService(processes, recording, soundLogger))
                {
                    sound.Start();
                    System.Threading.Thread.Sleep(700);

                    if (running.Count > 0)
                    {
                        int pid = running[0].ProcessId;
                        sound.SetInstanceVolume(pid, 0.5f);
                        check("单实例手动音量写入", recording.LastVolumes != null &&
                            recording.LastVolumes.ContainsKey(pid) &&
                            Math.Abs(recording.LastVolumes[pid] - 0.5f) < 0.01f);
                    }
                    else
                    {
                        check("单实例手动音量写入", true);
                    }

                    sound.Stop();
                }
                check("声音跟随服务扫描真实进程", recording.ApplyCalls > 0);
                if (running.Count > 0)
                {
                    check("关闭声音跟随后恢复原始音量", recording.LastVolumes != null &&
                        recording.LastVolumes.ContainsKey(running[0].ProcessId) &&
                        Math.Abs(recording.LastVolumes[running[0].ProcessId] - 0.8f) < 0.01f);
                }
                else
                {
                    check("关闭声音跟随后恢复原始音量", true);
                }

                using (HealthMonitorService health = new HealthMonitorService(windows, new AppLogger()))
                {
                    health.Start();
                    System.Threading.Thread.Sleep(300);
                    health.Stop();
                }
                check("健康监控启停不抛异常", true);

                try
                {
                    StartupManager.IsEnabled();
                    check("开机自启状态可读", true);
                }
                catch (Exception ex)
                {
                    check("开机自启状态可读", false);
                    output.AppendLine("       " + ex.Message);
                }

                GameLauncher launcher = new GameLauncher(processes);
                AppLogger launcherLogger = new AppLogger();
                LaunchResult invalid = launcher.LaunchAsync(
                    2,
                    Path.Combine(Path.GetTempPath(), "yys-missing-root-" + Guid.NewGuid().ToString("N")),
                    launcherLogger,
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                check("多开拒绝无效游戏目录", !string.IsNullOrEmpty(invalid.Error));
            }
            catch (Exception ex)
            {
                failed++;
                output.AppendLine("FAIL  自检过程异常：" + ex);
            }

            output.AppendLine(string.Format("RESULT passed={0} failed={1}", passed, failed));
            string text = output.ToString();
            Console.WriteLine(text);
            try
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.log"), text, new UTF8Encoding(false));
            }
            catch { }

            return failed == 0 ? 0 : 1;
        }

        private static void TestInputValidation(Action<string, bool> check)
        {
            int value;
            check("解析目标开数 3", InputValidation.TryParseInt("3", 1, 10, out value) && value == 3);
            check("拒绝目标开数 0", !InputValidation.TryParseInt("0", 1, 10, out value));
            check("拒绝目标开数 11", !InputValidation.TryParseInt("11", 1, 10, out value));
            check("拒绝非数字输入", !InputValidation.TryParseInt("abc", 1, 10, out value));
        }

        private static void TestGridLayout(Action<string, bool> check)
        {
            NativeMethods.RECT work = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };

            GridLayout one = GridLayout.Create(1, 1, work);
            check("单窗口单列布局", one.Rows == 1 && one.TargetWidth > 0);

            GridLayout four = GridLayout.Create(4, 2, work);
            check("四窗口两列布局", four.Rows == 2 && four.CellWidth == 960);

            GridLayout ten = GridLayout.Create(10, 4, work);
            check("十窗口四列布局", ten.Rows == 3);

            bool inBounds = true;
            for (int row = 0; row < ten.Rows; row++)
            {
                int y = ten.CellY(row, 500);
                if (y < work.Top || y + 500 > work.Bottom) inBounds = false;
            }
            check("窗口 Y 坐标不越界", inBounds);

            bool xInBounds = true;
            for (int column = 0; column < ten.Columns; column++)
            {
                int x = ten.CellX(column, ten.TargetWidth);
                if (x < work.Left || x + ten.TargetWidth > work.Right) xInBounds = false;
            }
            check("窗口 X 坐标不越界", xInBounds);
        }

        private static void TestWindowMatcher(Action<string, bool> check)
        {
            check("识别 onmyoji.exe 主窗口",
                GameWindowMatcher.IsGameWindow("阴阳师", @"C:\Netease\yys\bin\onmyoji.exe", 100, 1));
            check("拒绝旧版 yys.exe 进程",
                !GameWindowMatcher.IsGameWindow("阴阳师 - 账号 2", @"D:\Games\yys.exe", 101, 1));
            check("排除工具自身标题",
                !GameWindowMatcher.IsGameWindow("阴阳师工具", @"C:\Tools\阴阳师工具.exe", 102, 1));
            check("排除登录/支付窗口",
                !GameWindowMatcher.IsGameWindow("阴阳师登录", @"C:\Netease\yys\bin\onmyoji.exe", 103, 1));
            check("排除非游戏可执行文件",
                !GameWindowMatcher.IsGameWindow("阴阳师", @"C:\Windows\notepad.exe", 104, 1));
        }

        private static void TestSettingsStore(Action<string, bool> check)
        {
            string temp = Path.Combine(Path.GetTempPath(), "yys-selftest-" + Guid.NewGuid().ToString("N"));
            string gameRoot = Path.Combine(temp, "game");
            Directory.CreateDirectory(gameRoot);

            try
            {
                SettingsStore store = new SettingsStore(Path.Combine(temp, "config.ini"));
                AppSettings settings = new AppSettings
                {
                    GameRoot = gameRoot,
                    TargetInstances = 5,
                    Columns = 3,
                    MonitorIndex = 1,
                    BackgroundVolumePercent = 25,
                    HotkeyScheme = 1,
                    AutoRestartUnresponsive = true,
                    CompactMode = true,
                    DimUnfocusedWindows = true,
                    DarkTheme = true,
                    SoundFocusEnabled = false
                };

                string saveError;
                check("配置保存成功", store.TrySave(settings, out saveError));
                AppSettings loaded = store.Load();
                check("配置往返：游戏目录", loaded.GameRoot == gameRoot);
                check("配置往返：目标开数", loaded.TargetInstances == 5);
                check("配置往返：列数", loaded.Columns == 3);
                check("配置往返：显示器", loaded.MonitorIndex == 1);
                check("配置往返：失焦音量", loaded.BackgroundVolumePercent == 25);
                check("配置往返：快捷键方案", loaded.HotkeyScheme == 1);
                check("配置往返：自动重启", loaded.AutoRestartUnresponsive);
                check("配置往返：紧凑模式", loaded.CompactMode);
                check("配置往返：窗口透明度", loaded.DimUnfocusedWindows);
                check("配置往返：深色主题", loaded.DarkTheme);
                check("配置往返：声音开关", !loaded.SoundFocusEnabled);

                Directory.CreateDirectory(temp);
                string legacyPath = Path.Combine(temp, "legacy.ini");
                File.WriteAllText(legacyPath, gameRoot, new UTF8Encoding(false));
                SettingsStore legacyStore = new SettingsStore(legacyPath);
                AppSettings legacy = legacyStore.Load();
                check("旧版单行配置不再读取", legacy.GameRoot == AppSettings.DefaultGameRoot);
                check("无效配置给出错误提示", !string.IsNullOrEmpty(legacyStore.LastLoadError));
                check("无效配置回退默认开数", legacy.TargetInstances == 2);
            }
            catch (Exception ex)
            {
                check("配置测试不抛异常", false);
                check("配置测试异常：" + ex.Message, false);
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
            }
        }

        private static void TestLayoutPresets(Action<string, bool> check)
        {
            string temp = Path.Combine(Path.GetTempPath(), "yys-preset-" + Guid.NewGuid().ToString("N"));
            try
            {
                LayoutPresetStore store = new LayoutPresetStore(Path.Combine(temp, "layouts.ini"));
                List<LayoutPreset> presets = new List<LayoutPreset>();
                presets.Add(new LayoutPreset { Name = "日常 4 开", Columns = 4, MonitorIndex = 1 });
                presets.Add(new LayoutPreset { Name = "双开", Columns = 2, MonitorIndex = 0 });
                string error;
                check("布局预设保存成功", store.Save(presets, out error));

                List<LayoutPreset> loaded = store.Load();
                check("布局预设数量往返", loaded.Count == 2);
                check("布局预设名称往返", loaded.Count > 0 && loaded[0].Name == "日常 4 开");
                check("布局预设列数往返", loaded.Count > 0 && loaded[0].Columns == 4);
                check("布局预设显示器往返", loaded.Count > 0 && loaded[0].MonitorIndex == 1);
                check("拒绝非法预设名", !LayoutPresetStore.IsValidName("[bad]"));
            }
            catch (Exception ex)
            {
                check("布局预设测试不抛异常", false);
                check("布局预设测试异常：" + ex.Message, false);
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
            }
        }

        private static void TestMonitors(Action<string, bool> check)
        {
            try
            {
                MonitorService monitors = new MonitorService();
                IReadOnlyList<MonitorInfo> list = monitors.GetMonitors();
                check("枚举显示器不抛异常", list.Count > 0);
                check("显示器工作区有效", list.Count > 0 && !list[0].WorkArea.IsEmpty);
            }
            catch (Exception ex)
            {
                check("枚举显示器不抛异常", false);
                check("显示器枚举异常：" + ex.Message, false);
            }
        }

        private static void TestWindowPlacements(Action<string, bool> check)
        {
            string temp = Path.Combine(Path.GetTempPath(), "yys-placement-" + Guid.NewGuid().ToString("N"));
            try
            {
                WindowPlacementStore store = new WindowPlacementStore(Path.Combine(temp, "placements.ini"));
                List<GameWindowInfo> windows = new List<GameWindowInfo>();
                windows.Add(new GameWindowInfo
                {
                    Title = "阴阳师 - 测试",
                    Bounds = new NativeMethods.RECT { Left = 10, Top = 20, Right = 810, Bottom = 620 }
                });
                string error;
                check("窗口位置保存成功", store.Save(windows, out error));
                Dictionary<string, NativeMethods.RECT> loaded = store.Load();
                NativeMethods.RECT bounds;
                check("窗口位置数量往返", loaded.Count == 1);
                check("窗口位置坐标往返", loaded.TryGetValue("阴阳师 - 测试", out bounds) &&
                    bounds.Left == 10 && bounds.Top == 20 && bounds.Width == 800 && bounds.Height == 600);
            }
            catch (Exception ex)
            {
                check("窗口位置测试不抛异常", false);
                check("窗口位置测试异常：" + ex.Message, false);
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
            }
        }

        private static void TestInstanceLabels(Action<string, bool> check)
        {
            string temp = Path.Combine(Path.GetTempPath(), "yys-label-" + Guid.NewGuid().ToString("N"));
            try
            {
                InstanceLabelStore store = new InstanceLabelStore(Path.Combine(temp, "labels.ini"));
                string error;
                check("实例标签保存成功", store.Set("阴阳师 - 测试", "主号", out error));
                check("实例标签读取", store.Get("阴阳师 - 测试") == "主号");
                check("实例标签清除", store.Set("阴阳师 - 测试", "", out error) &&
                    string.IsNullOrEmpty(store.Get("阴阳师 - 测试")));
            }
            catch (Exception ex)
            {
                check("实例标签测试不抛异常", false);
                check("实例标签测试异常：" + ex.Message, false);
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
            }
        }

        private sealed class RecordingAudioService : IAudioSessionService
        {
            public int ApplyCalls;
            public Dictionary<int, float> LastVolumes;

            public HashSet<int> ApplyVolumes(IDictionary<int, float> desiredVolumes)
            {
                System.Threading.Interlocked.Increment(ref ApplyCalls);
                LastVolumes = new Dictionary<int, float>();
                HashSet<int> applied = new HashSet<int>();
                if (desiredVolumes != null)
                {
                    foreach (int processId in desiredVolumes.Keys)
                    {
                        applied.Add(processId);
                        LastVolumes[processId] = desiredVolumes[processId];
                    }
                }
                return applied;
            }

            public Dictionary<int, float> GetVolumes(IEnumerable<int> processIds)
            {
                Dictionary<int, float> volumes = new Dictionary<int, float>();
                if (processIds != null)
                    foreach (int processId in processIds) volumes[processId] = 0.8f;
                return volumes;
            }
        }
    }
}

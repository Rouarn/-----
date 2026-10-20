using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OnmyojiTools.App
{
    /// <summary>
    /// config.ini 的读写。兼容旧版“文件里只有一行游戏目录”的格式；
    /// 新格式为 key=value，采用临时文件 + 替换的原子写入。
    /// </summary>
    internal sealed class SettingsStore
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly string _path;

        public SettingsStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("配置路径不能为空", "path");
            _path = path;
        }

        public string FilePath { get { return _path; } }

        public AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                if (!File.Exists(_path)) return settings;

                string[] lines = File.ReadAllLines(_path, Encoding.UTF8);
                bool sawKey = false;
                bool sawLegacyRoot = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;

                    int equals = line.IndexOf('=');
                    if (equals < 0)
                    {
                        // 旧格式：整行就是一个目录
                        if (!sawKey && !sawLegacyRoot && Directory.Exists(line))
                        {
                            settings.GameRoot = line;
                            sawLegacyRoot = true;
                        }
                        continue;
                    }

                    sawKey = true;
                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();
                    if (key.Length == 0) continue;

                    switch (key.ToLowerInvariant())
                    {
                        case "gameroot":
                            if (value.Length > 0) settings.GameRoot = value;
                            break;
                        case "targetinstances":
                            settings.TargetInstances = ParseInt(value, settings.TargetInstances);
                            break;
                        case "columns":
                            settings.Columns = ParseInt(value, settings.Columns);
                            break;
                        case "soundfocusenabled":
                            settings.SoundFocusEnabled = ParseBool(value, settings.SoundFocusEnabled);
                            break;
                    }
                }
            }
            catch
            {
                // 配置损坏时回退到默认值，不能阻止程序启动
            }

            settings.Normalize();
            return settings;
        }

        public bool TrySave(AppSettings settings, out string error)
        {
            error = null;
            if (settings == null) { error = "配置对象为空"; return false; }

            settings.Normalize();
            string temp = null;
            try
            {
                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("# 阴阳师工具配置");
                builder.AppendLine("GameRoot=" + settings.GameRoot);
                builder.AppendLine("TargetInstances=" + settings.TargetInstances);
                builder.AppendLine("Columns=" + settings.Columns);
                builder.AppendLine("SoundFocusEnabled=" + (settings.SoundFocusEnabled ? "true" : "false"));

                temp = _path + ".tmp";
                File.WriteAllText(temp, builder.ToString(), Utf8NoBom);

                if (File.Exists(_path))
                {
                    try { File.Replace(temp, _path, null); }
                    catch
                    {
                        File.Copy(temp, _path, true);
                        File.Delete(temp);
                    }
                }
                else
                {
                    File.Move(temp, _path);
                }
                temp = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (temp != null)
                {
                    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                }
            }
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }
    }
}

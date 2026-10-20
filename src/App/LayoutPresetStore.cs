using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OnmyojiTools.Core;

namespace OnmyojiTools.App
{
    internal sealed class LayoutPreset
    {
        public string Name { get; set; }
        public int Columns { get; set; }
        public int MonitorIndex { get; set; }
    }

    /// <summary>
    /// layouts.ini：按 [名称] + Columns=N 保存布局预设。
    /// 只保存当前实现需要的列数；显示器/槽位等字段可在后续版本追加。
    /// </summary>
    internal sealed class LayoutPresetStore
    {
        private const int MaxPresets = 12;
        private const int MaxNameLength = 20;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly string _path;

        public LayoutPresetStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("布局预设路径不能为空", "path");
            _path = path;
        }

        public string FilePath { get { return _path; } }

        public static bool IsValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string value = name.Trim();
            if (value.Length == 0 || value.Length > MaxNameLength) return false;
            if (value.IndexOf('[') >= 0 || value.IndexOf(']') >= 0) return false;
            if (value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0) return false;
            return true;
        }

        public List<LayoutPreset> Load()
        {
            List<LayoutPreset> result = new List<LayoutPreset>();
            if (!File.Exists(_path)) return result;

            string currentName = null;
            int currentColumns = 0;
            int currentMonitor = 0;
            Action flush = delegate
            {
                if (currentName == null) return;
                bool duplicate = false;
                for (int j = 0; j < result.Count; j++)
                {
                    if (!string.Equals(result[j].Name, currentName, StringComparison.OrdinalIgnoreCase)) continue;
                    duplicate = true;
                    break;
                }
                if (!duplicate && result.Count < MaxPresets)
                {
                    result.Add(new LayoutPreset
                    {
                        Name = currentName,
                        Columns = currentColumns == 0 ? 2 : currentColumns,
                        MonitorIndex = currentMonitor
                    });
                }
                currentName = null;
                currentColumns = 0;
                currentMonitor = 0;
            };

            try
            {
                string[] lines = File.ReadAllLines(_path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;

                    if (line.StartsWith("[") && line.EndsWith("]") && line.Length > 2)
                    {
                        flush();
                        string name = line.Substring(1, line.Length - 2).Trim();
                        currentName = IsValidName(name) ? name : null;
                        currentColumns = 0;
                        currentMonitor = 0;
                        continue;
                    }

                    if (currentName == null) continue;
                    int equals = line.IndexOf('=');
                    if (equals <= 0) continue;
                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();
                    if (string.Equals(key, "Columns", StringComparison.OrdinalIgnoreCase))
                    {
                        int columns;
                        if (!int.TryParse(value, out columns)) continue;
                        currentColumns = AppSettings.Clamp(columns, AppSettings.MinColumns, AppSettings.MaxColumns);
                    }
                    else if (string.Equals(key, "Monitor", StringComparison.OrdinalIgnoreCase))
                    {
                        int monitor;
                        if (int.TryParse(value, out monitor) && monitor >= 0) currentMonitor = monitor;
                    }
                }
                flush();
            }
            catch
            {
                return new List<LayoutPreset>();
            }
            return result;
        }

        public bool Save(IList<LayoutPreset> presets, out string error)
        {
            error = null;
            string temp = null;
            try
            {
                List<LayoutPreset> clean = new List<LayoutPreset>();
                if (presets != null)
                {
                    for (int i = 0; i < presets.Count && clean.Count < MaxPresets; i++)
                    {
                        LayoutPreset preset = presets[i];
                        if (preset == null || !IsValidName(preset.Name)) continue;

                        string name = preset.Name.Trim();
                        bool duplicate = false;
                        for (int j = 0; j < clean.Count; j++)
                        {
                            if (string.Equals(clean[j].Name, name, StringComparison.OrdinalIgnoreCase))
                            {
                                duplicate = true;
                                break;
                            }
                        }
                        if (duplicate) continue;

                        clean.Add(new LayoutPreset
                        {
                            Name = name,
                            Columns = AppSettings.Clamp(preset.Columns, AppSettings.MinColumns, AppSettings.MaxColumns),
                            MonitorIndex = preset.MonitorIndex < 0 ? 0 : preset.MonitorIndex
                        });
                    }
                }

                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("# 阴阳师工具布局预设");
                for (int i = 0; i < clean.Count; i++)
                {
                    builder.AppendLine("[" + clean[i].Name + "]");
                    builder.AppendLine("Columns=" + clean[i].Columns);
                    builder.AppendLine("Monitor=" + clean[i].MonitorIndex);
                }

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
                    try { if (File.Exists(temp)) File.Delete(temp); }
                    catch { }
                }
            }
        }
    }
}

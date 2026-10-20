using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OnmyojiTools.App
{
    /// <summary>labels.ini：按窗口标题保存用户标签/备注；Base64 避免标题和备注中的分隔符破坏格式。</summary>
    internal sealed class InstanceLabelStore
    {
        private const int MaxEntries = 32;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly string _path;

        public InstanceLabelStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("标签路径不能为空", "path");
            _path = path;
        }

        public string FilePath { get { return _path; } }

        public Dictionary<string, string> Load()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(_path)) return map;
            try
            {
                string[] lines = File.ReadAllLines(_path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int split = line.IndexOf('|');
                    if (split <= 0) continue;

                    string title;
                    string label;
                    try
                    {
                        title = Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(0, split)));
                        label = Encoding.UTF8.GetString(Convert.FromBase64String(line.Substring(split + 1)));
                    }
                    catch { continue; }
                    if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(label)) continue;
                    map[title] = label;
                    if (map.Count >= MaxEntries) break;
                }
            }
            catch { map.Clear(); }
            return map;
        }

        public string Get(string title)
        {
            if (string.IsNullOrEmpty(title)) return null;
            string label;
            return Load().TryGetValue(title, out label) ? label : null;
        }

        public bool Set(string title, string label, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(title))
            {
                error = "窗口标题为空。";
                return false;
            }
            try
            {
                Dictionary<string, string> map = Load();
                if (string.IsNullOrWhiteSpace(label)) map.Remove(title);
                else map[title] = label.Trim();
                if (map.Count > MaxEntries)
                {
                    error = "标签数量超过上限。";
                    return false;
                }

                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("# 阴阳师工具实例标签");
                foreach (KeyValuePair<string, string> pair in map)
                {
                    builder.AppendLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Key)) + "|" +
                        Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Value)));
                }

                string temp = _path + ".tmp";
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
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OnmyojiTools.Core;
using OnmyojiTools.Interop;

namespace OnmyojiTools.App
{
    /// <summary>
    /// placements.ini：按窗口标题记住上次位置。
    /// 每行 Base64(标题)|Left|Top|Width|Height，避免标题里的方括号破坏格式。
    /// </summary>
    internal sealed class WindowPlacementStore
    {
        private const int MaxEntries = 32;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly string _path;

        public WindowPlacementStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("窗口位置路径不能为空", "path");
            _path = path;
        }

        public string FilePath { get { return _path; } }

        public bool Save(IReadOnlyList<GameWindowInfo> windows, out string error)
        {
            error = null;
            string temp = null;
            try
            {
                Dictionary<string, NativeMethods.RECT> map =
                    new Dictionary<string, NativeMethods.RECT>(StringComparer.OrdinalIgnoreCase);
                if (windows != null)
                {
                    for (int i = 0; i < windows.Count && map.Count < MaxEntries; i++)
                    {
                        GameWindowInfo window = windows[i];
                        if (window == null || string.IsNullOrEmpty(window.Title)) continue;
                        if (window.Bounds.IsEmpty) continue;
                        map[window.Title] = window.Bounds;
                    }
                }

                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("# 阴阳师工具窗口位置记忆");
                foreach (KeyValuePair<string, NativeMethods.RECT> pair in map)
                {
                    string title = Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Key));
                    builder.AppendLine(title + "|" + pair.Value.Left + "|" + pair.Value.Top + "|" +
                        pair.Value.Width + "|" + pair.Value.Height);
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

        public Dictionary<string, NativeMethods.RECT> Load()
        {
            Dictionary<string, NativeMethods.RECT> map =
                new Dictionary<string, NativeMethods.RECT>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(_path)) return map;

            try
            {
                string[] lines = File.ReadAllLines(_path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length != 5) continue;

                    string title;
                    try { title = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0])); }
                    catch { continue; }
                    if (string.IsNullOrEmpty(title)) continue;

                    int left, top, width, height;
                    if (!int.TryParse(parts[1], out left) || !int.TryParse(parts[2], out top) ||
                        !int.TryParse(parts[3], out width) || !int.TryParse(parts[4], out height)) continue;
                    if (width <= 0 || height <= 0) continue;

                    map[title] = new NativeMethods.RECT
                    {
                        Left = left,
                        Top = top,
                        Right = left + width,
                        Bottom = top + height
                    };
                    if (map.Count >= MaxEntries) break;
                }
            }
            catch
            {
                map.Clear();
            }
            return map;
        }

        public int Apply(IReadOnlyList<GameWindowInfo> windows, out string error)
        {
            error = null;
            if (windows == null) return 0;
            Dictionary<string, NativeMethods.RECT> map = Load();
            if (map.Count == 0) return 0;

            int applied = 0;
            for (int i = 0; i < windows.Count; i++)
            {
                GameWindowInfo window = windows[i];
                if (window == null || window.Handle == IntPtr.Zero) continue;
                NativeMethods.RECT bounds;
                if (!map.TryGetValue(window.Title ?? "", out bounds)) continue;
                if (!NativeMethods.IsWindow(window.Handle)) continue;

                try
                {
                    NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_RESTORE);
                    bool ok = NativeMethods.SetWindowPos(window.Handle, IntPtr.Zero,
                        bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                        NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                    if (ok) applied++;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
            }
            return applied;
        }
    }
}

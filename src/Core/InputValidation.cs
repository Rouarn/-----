using System;
using System.IO;

namespace OnmyojiTools.Core
{
    /// <summary>输入与路径校验（纯函数）。</summary>
    internal static class InputValidation
    {
        public const int MinInstances = 1;
        public const int MaxInstances = 10;
        public const int MinColumns = 1;
        public const int MaxColumns = 4;

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static bool TryParseInt(string text, int min, int max, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (!int.TryParse(text.Trim(), out value)) return false;
            return value >= min && value <= max;
        }

        public static bool TryValidateGameRoot(string root, out string executablePath, out string error)
        {
            executablePath = null;
            error = null;

            if (string.IsNullOrWhiteSpace(root))
            {
                error = "游戏目录不能为空。";
                return false;
            }

            string trimmed;
            try { trimmed = Path.GetFullPath(root.Trim()); }
            catch (Exception ex)
            {
                error = "游戏目录无效：" + ex.Message;
                return false;
            }

            if (!Directory.Exists(trimmed))
            {
                error = "游戏目录不存在：" + trimmed;
                return false;
            }

            string exe = Path.Combine(trimmed, "bin", "onmyoji.exe");
            if (!File.Exists(exe))
            {
                error = "目录中未找到 bin\\onmyoji.exe：" + trimmed;
                return false;
            }

            executablePath = exe;
            return true;
        }
    }
}

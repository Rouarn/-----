using System;
using System.IO;

namespace OnmyojiTools.Core
{
    /// <summary>窗口/进程识别规则（纯函数，便于自检）。</summary>
    internal static class GameWindowMatcher
    {
        private static readonly string[] ExcludedTitleParts =
        {
            "mpay", "pay", "tips", "form", "login", "dialog", "popup",
            "notice", "menu", "setting", "dlg", "wnd", "排列", "工具",
            "登录", "支付", "充值", "公告", "提示", "菜单", "设置", "弹窗", "对话框"
        };

        public static bool IsGameExecutable(string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath)) return false;
            string file;
            try { file = Path.GetFileName(executablePath); }
            catch { return false; }

            return string.Equals(file, "onmyoji.exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(file, "yys.exe", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsGameTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return false;
            if (title.IndexOf("阴阳师", StringComparison.Ordinal) < 0) return false;

            string lower = title.ToLowerInvariant();
            for (int i = 0; i < ExcludedTitleParts.Length; i++)
                if (lower.IndexOf(ExcludedTitleParts[i], StringComparison.Ordinal) >= 0) return false;

            return true;
        }

        public static bool IsGameWindow(string title, string executablePath, int processId, int selfProcessId)
        {
            if (processId <= 0 || processId == selfProcessId) return false;
            if (!IsGameTitle(title)) return false;
            // 拿不到路径时（权限不足）退化为标题判断；拿到路径时必须是游戏可执行文件。
            if (!string.IsNullOrEmpty(executablePath) && !IsGameExecutable(executablePath)) return false;
            return true;
        }
    }
}

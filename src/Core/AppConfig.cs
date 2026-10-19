using System;
using System.IO;
using System.Text;

namespace OnmyojiTools.Core
{
    /// <summary>应用配置：config.ini 仅保存一行游戏安装根目录</summary>
    internal class AppConfig
    {
        public const string DefaultRoot = @"D:\Netease\yys";

        private static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini"); }
        }

        /// <summary>游戏安装根目录（含 bin\onmyoji.exe）</summary>
        public string YysRoot = DefaultRoot;

        /// <summary>从 config.ini 读取根目录（目录不存在时保持默认，静默忽略异常）</summary>
        public void Load()
        {
            try
            {
                string cfg = FilePath;
                if (File.Exists(cfg))
                {
                    string line = File.ReadAllText(cfg, Encoding.UTF8).Trim();
                    if (line.Length > 0 && Directory.Exists(line))
                    {
                        YysRoot = line;
                    }
                }
            }
            catch { }
        }

        /// <summary>保存配置到 config.ini，失败时返回错误信息（成功返回 null）</summary>
        public string Save()
        {
            try { File.WriteAllText(FilePath, YysRoot, Encoding.UTF8); return null; }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>校验根目录有效性（必须存在 bin\onmyoji.exe），无效时输出日志</summary>
        public bool Validate(Action<string> log)
        {
            string exe = Path.Combine(YysRoot, "bin", "onmyoji.exe");
            if (Directory.Exists(YysRoot) && File.Exists(exe)) return true;
            log("工作目录无效：" + YysRoot + "（未找到 bin\\onmyoji.exe，请点「浏览…」重新选择）");
            return false;
        }
    }
}

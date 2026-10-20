using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace OnmyojiTools.App
{
    internal sealed class UpdateInfo
    {
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string ReleaseUrl { get; set; }
        public string DownloadUrl { get; set; }
        public bool HasUpdate { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// 通过 GitHub Releases API 检查新版本，只提示并打开下载页，不自动替换 exe。
    /// 网络不可用/仓库没有 Release 时返回错误，不影响主流程。
    /// </summary>
    internal static class UpdateChecker
    {
        public const string CurrentVersion = "3.0";
        private const string ReleasesApi = "https://api.github.com/repos/Rouarn/OnmyojiTools/releases/latest";

        public static UpdateInfo Check()
        {
            UpdateInfo result = new UpdateInfo { CurrentVersion = CurrentVersion };
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ReleasesApi);
                request.UserAgent = "OnmyojiTools/" + CurrentVersion;
                request.Accept = "application/vnd.github+json";
                request.Timeout = 8000;
                request.ReadWriteTimeout = 8000;

                string json;
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    json = reader.ReadToEnd();
                }

                Match tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                Match url = Regex.Match(json, "\"html_url\"\\s*:\\s*\"([^\"]+)\"");
                if (!tag.Success)
                {
                    result.Error = "发布信息中没有版本号。";
                    return result;
                }

                string latest = tag.Groups[1].Value.Trim();
                result.LatestVersion = latest;
                result.ReleaseUrl = url.Success ? url.Groups[1].Value : "https://github.com/Rouarn/OnmyojiTools/releases";

                foreach (Match asset in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
                {
                    string candidate = asset.Groups[1].Value;
                    if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        result.DownloadUrl = candidate;
                        break;
                    }
                }

                Version current;
                Version remote;
                if (!Version.TryParse(CurrentVersion, out current) ||
                    !Version.TryParse(latest.TrimStart('v', 'V'), out remote))
                {
                    result.Error = "无法解析版本号：" + latest;
                    return result;
                }

                result.HasUpdate = remote > current;
                return result;
            }
            catch (WebException ex)
            {
                result.Error = ex.Status == WebExceptionStatus.ProtocolError
                    ? "仓库暂无 Release 或网络请求被拒绝。"
                    : "检查更新失败：" + ex.Message;
            }
            catch (Exception ex)
            {
                result.Error = "检查更新失败：" + ex.Message;
            }
            return result;
        }
    }
}

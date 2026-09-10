using System;
using System.Reflection;
using System.Threading.Tasks;

namespace AvaloniaTodoApp.Services;

/// <summary>
/// 版本检测服务
/// 负责检测应用最新版本、更新日志和下载链接
/// </summary>
public class UpdateService : IUpdateService
{
    public string CurrentVersion
    {
        get
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
        }
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync()
    {
        // 模拟网络请求延迟（实际项目中可换成 HttpClient 请求 GitHub Releases API 或业务接口）
        await Task.Delay(600);

        var current = CurrentVersion;
        // 演示环境：我们展示一个准备好的 v1.1.0 升级包
        var latest = "1.1.0";
        var hasUpdate = IsNewerVersion(latest, current);

        return new UpdateInfo
        {
            CurrentVersion = current,
            LatestVersion = latest,
            HasUpdate = hasUpdate,
            ReleaseTitle = "v1.1.0 性能与体验优化更新",
            ReleaseNotes = "• 新增：跨平台系统托盘与关闭常驻保护\n" +
                           "• 新增：本地任务数据自动持久化与原子落盘保护\n" +
                           "• 新增：版本检测与关于弹窗基础建设\n" +
                           "• 优化：MVVM 依赖注入架构，启动更轻快\n" +
                           "• 修复：macOS 和 Windows 跨平台明暗主题自适应细节",
            ReleaseDate = DateTimeOffset.Now,
            DownloadUrl = "https://github.com/avaloniaui/avalonia"
        };
    }

    /// <summary>
    /// 稳健的语义版本数值比对算法，杜绝纯字符串字典序比较导致的 '1.10.0' < '1.9.0' 倒挂缺陷
    /// </summary>
    public static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(latestVersion)) return false;
        if (string.IsNullOrWhiteSpace(currentVersion)) return true;

        var cleanLatest = latestVersion.Trim().TrimStart('v', 'V');
        var cleanCurrent = currentVersion.Trim().TrimStart('v', 'V');

        if (Version.TryParse(cleanLatest, out var vLatest) && Version.TryParse(cleanCurrent, out var vCurrent))
        {
            return vLatest > vCurrent;
        }

        // 兜底：处理非标准三段/带后缀的版本号
        var latestParts = cleanLatest.Split('-')[0].Split('.');
        var currentParts = cleanCurrent.Split('-')[0].Split('.');
        var maxLen = Math.Max(latestParts.Length, currentParts.Length);

        for (int i = 0; i < maxLen; i++)
        {
            int lVal = (i < latestParts.Length && int.TryParse(latestParts[i], out var lv)) ? lv : 0;
            int cVal = (i < currentParts.Length && int.TryParse(currentParts[i], out var cv)) ? cv : 0;
            if (lVal != cVal)
            {
                return lVal > cVal;
            }
        }

        return false;
    }
}

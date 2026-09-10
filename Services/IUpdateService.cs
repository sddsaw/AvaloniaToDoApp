using System;
using System.Threading.Tasks;

namespace AvaloniaTodoApp.Services;

public class UpdateInfo
{
    public string CurrentVersion { get; set; } = "1.0.0";
    public string LatestVersion { get; set; } = "1.0.0";
    public bool HasUpdate { get; set; }
    public string ReleaseTitle { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public DateTimeOffset ReleaseDate { get; set; } = DateTimeOffset.Now;
    public string DownloadUrl { get; set; } = string.Empty;
}

public interface IUpdateService
{
    string CurrentVersion { get; }
    Task<UpdateInfo> CheckForUpdatesAsync();
}

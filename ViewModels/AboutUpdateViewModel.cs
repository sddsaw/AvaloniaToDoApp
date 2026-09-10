using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AvaloniaTodoApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaTodoApp.ViewModels;

public partial class AboutUpdateViewModel : ViewModelBase
{
    private readonly IUpdateService _updateService;
    private readonly ITodoStorageService _storageService;

    [ObservableProperty]
    private string _currentVersion;

    [ObservableProperty]
    private string _latestVersion = string.Empty;

    [ObservableProperty]
    private bool _hasUpdate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CheckButtonText))]
    private bool _isChecking;

    public string CheckButtonText => IsChecking ? "正在检查..." : "检查更新";

    [ObservableProperty]
    private string _statusMessage = "点击下方按钮检查更新";

    [ObservableProperty]
    private string _releaseTitle = string.Empty;

    [ObservableProperty]
    private string _releaseNotes = string.Empty;

    [ObservableProperty]
    private string _storagePath;

    [ObservableProperty]
    private string _logFolderPath;

    private string _downloadUrl = "https://github.com/avaloniaui/avalonia";

    public AboutUpdateViewModel(IUpdateService updateService, ITodoStorageService storageService)
    {
        _updateService = updateService;
        _storageService = storageService;

        CurrentVersion = $"v{_updateService.CurrentVersion}";
        StoragePath = _storageService.StorageFilePath;
        LogFolderPath = LogHelper.LogDirectoryPath;
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        LogHelper.OpenLogFolder();
    }

    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        if (IsChecking) return;

        try
        {
            IsChecking = true;
            StatusMessage = "正在连接更新服务器，请稍候...";

            var info = await _updateService.CheckForUpdatesAsync();

            LatestVersion = $"v{info.LatestVersion}";
            HasUpdate = info.HasUpdate;
            ReleaseTitle = info.ReleaseTitle;
            ReleaseNotes = info.ReleaseNotes;
            _downloadUrl = info.DownloadUrl;

            StatusMessage = info.HasUpdate
                ? $"发现新版本 {info.LatestVersion}！建议更新以获取更佳体验。"
                : $"当前已是最新版本 ({CurrentVersion})，无需更新。";

            Serilog.Log.Information("[Update] 检测完成: HasUpdate={HasUpdate}, Latest={Latest}", info.HasUpdate, info.LatestVersion);
        }
        catch (Exception ex)
        {
            StatusMessage = $"检查更新失败: {ex.Message}";
            Serilog.Log.Error(ex, "[Update] 检查更新时发生网络或服务异常");
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private void OpenDownloadUrl()
    {
        if (string.IsNullOrWhiteSpace(_downloadUrl))
            return;

        try
        {
            var url = _downloadUrl;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[UI] 无法打开更新下载链接: {Url}", _downloadUrl);
        }
    }
}

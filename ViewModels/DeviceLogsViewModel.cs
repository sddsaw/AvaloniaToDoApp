using AvaloniaTodoApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaTodoApp.ViewModels;

/// <summary>
/// 工业设备运行与系统日志 ViewModel
/// </summary>
public partial class DeviceLogsViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _logFolderPath;

    [ObservableProperty]
    private string _logMode = "Console + File 双写滚动归档";

    [ObservableProperty]
    private int _retentionDays = 31;

    public DeviceLogsViewModel()
    {
        _logFolderPath = LogHelper.LogDirectoryPath;
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        LogHelper.OpenLogFolder();
    }
}

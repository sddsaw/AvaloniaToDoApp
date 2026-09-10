using System;
using System.Threading.Tasks;
using AvaloniaTodoApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaTodoApp.ViewModels;

/// <summary>
/// 侧边栏导航业务模块枚举
/// </summary>
public enum NavModule
{
    ChipInspect, // 3D 芯片检测与上位机看板
    Todo,        // 生产工单与待办任务
    Logs         // 工业设备与运行日志
}

/// <summary>
/// 主界面宿主与路由外壳 ViewModel (类似 Vue 的 AppShell / RouterLayout 控制器)
/// 负责全局侧边栏导航、生命周期协调，通过 ViewLocator + ContentControl 实现路由映射
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // 子模块单例 ViewModel (由 DI 容器统一注入，保持内存活)
    public ChipInspectViewModel ChipInspectVm { get; }
    public TodoViewModel TodoVm { get; }
    public DeviceLogsViewModel DeviceLogsVm { get; }

    private readonly IUpdateService _updateService;

    // 【路由出口绑定】当前激活渲染的子页面 ViewModel
    [ObservableProperty]
    private ViewModelBase _currentPage;

    // 当前导航枚举，派生导航按钮激活态 (通过 NotifyPropertyChangedFor 自动同步)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModuleChipInspect), nameof(IsModuleTodo), nameof(IsModuleLogs))]
    private NavModule _currentModule = NavModule.ChipInspect;

    public bool IsModuleChipInspect => CurrentModule == NavModule.ChipInspect;
    public bool IsModuleTodo => CurrentModule == NavModule.Todo;
    public bool IsModuleLogs => CurrentModule == NavModule.Logs;

    public string AppVersion => $"v{_updateService.CurrentVersion}";

    public event Action? RequestOpenAbout;

    public MainWindowViewModel(
        ChipInspectViewModel chipInspectVm,
        TodoViewModel todoVm,
        DeviceLogsViewModel deviceLogsVm,
        IUpdateService updateService)
    {
        ChipInspectVm = chipInspectVm;
        TodoVm = todoVm;
        DeviceLogsVm = deviceLogsVm;
        _updateService = updateService;

        // 默认进入 3D 芯片检测看板
        _currentPage = ChipInspectVm;
    }

    /// <summary>
    /// 路由切换指令 (类似 router.push(name))
    /// </summary>
    [RelayCommand]
    private void SwitchModule(string moduleName)
    {
        if (Enum.TryParse<NavModule>(moduleName, true, out var mod) && mod != CurrentModule)
        {
            CurrentModule = mod;
            CurrentPage = mod switch
            {
                NavModule.ChipInspect => ChipInspectVm,
                NavModule.Todo => TodoVm,
                NavModule.Logs => DeviceLogsVm,
                _ => ChipInspectVm
            };
            Serilog.Log.Information("[Router] 视图路由导航至: {Page}", CurrentPage.GetType().Name);
        }
    }

    [RelayCommand]
    private void OpenAbout()
    {
        RequestOpenAbout?.Invoke();
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        LogHelper.OpenLogFolder();
    }

    #region 生命周期托管 (Lifecycle)

    /// <summary>
    /// 窗口呈现后的异步初始化引导
    /// </summary>
    public Task InitializeAsync() => TodoVm.InitializeAsync();

    /// <summary>
    /// 窗口关闭前的脏数据安全落盘
    /// </summary>
    public Task FlushAsync() => TodoVm.FlushAsync();

    #endregion
}

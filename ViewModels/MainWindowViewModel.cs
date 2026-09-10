using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AvaloniaTodoApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaTodoApp.ViewModels;

public enum NavModule
{
    ChipInspect, // 3D 芯片检测与上位机看板
    Todo,        // 生产待办工单
    Logs         // 工业设备日志
}

/// <summary>
/// 主界面路由宿主与外壳 ViewModel (类似 Vue 的 Layout/AppShell 路由控制器)
/// 负责全局侧边栏导航、当前路由页面状态驱动，通过 ViewLocator + ContentControl 实现路由映射
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // 子模块单例 ViewModel（保活实例）
    public ChipInspectViewModel ChipInspectVm { get; }
    public TodoViewModel TodoVm { get; }
    public DeviceLogsViewModel DeviceLogsVm { get; }

    // 【路由出口绑定】当前激活渲染的子页面 ViewModel（等价于前端路由的 CurrentRoute / Component）
    [ObservableProperty]
    private ViewModelBase _currentPage;

    // 当前导航枚举
    [ObservableProperty]
    private NavModule _currentModule = NavModule.ChipInspect;

    public bool IsModuleChipInspect => CurrentModule == NavModule.ChipInspect;
    public bool IsModuleTodo => CurrentModule == NavModule.Todo;
    public bool IsModuleLogs => CurrentModule == NavModule.Logs;

    public string AppVersion => $"v{_updateService.CurrentVersion}";

    public event Action? RequestOpenAbout;

    private readonly IUpdateService _updateService;

    public MainWindowViewModel(ITodoStorageService storageService, IUpdateService updateService)
    {
        _updateService = updateService;

        ChipInspectVm = new ChipInspectViewModel();
        TodoVm = new TodoViewModel(storageService, updateService);
        DeviceLogsVm = new DeviceLogsViewModel();

        // 默认进入 3D 芯片全业务检测看板
        _currentPage = ChipInspectVm;
    }

    /// <summary>
    /// 路由切换指令 (类似 router.push(name))
    /// </summary>
    [RelayCommand]
    private void SwitchModule(string moduleName)
    {
        if (Enum.TryParse<NavModule>(moduleName, true, out var mod))
        {
            CurrentModule = mod;
            CurrentPage = mod switch
            {
                NavModule.ChipInspect => ChipInspectVm,
                NavModule.Todo => TodoVm,
                NavModule.Logs => DeviceLogsVm,
                _ => ChipInspectVm
            };
            OnPropertyChanged(nameof(IsModuleChipInspect));
            OnPropertyChanged(nameof(IsModuleTodo));
            OnPropertyChanged(nameof(IsModuleLogs));
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

    #region 单元测试与向后兼容透传委托 (Forwarding to TodoVm)

    public Task InitializeAsync() => TodoVm.InitializeAsync();
    public Task FlushAsync() => TodoVm.FlushAsync();

    public ObservableCollection<TodoItemViewModel> DisplayTasks => TodoVm.DisplayTasks;

    public string NewTaskTitle
    {
        get => TodoVm.NewTaskTitle;
        set => TodoVm.NewTaskTitle = value;
    }

    public TodoFilter CurrentFilter => TodoVm.CurrentFilter;
    public bool IsLoading => TodoVm.IsLoading;
    public int TotalCount => TodoVm.TotalCount;
    public int RemainingCount => TodoVm.RemainingCount;
    public int CompletedCount => TodoVm.CompletedCount;
    public string StatusSummary => TodoVm.StatusSummary;
    public string StorageStatus => TodoVm.StorageStatus;

    public IRelayCommand AddTaskCommand => TodoVm.AddTaskCommand;
    public IRelayCommand<TodoItemViewModel?> RemoveTaskCommand => TodoVm.RemoveTaskCommand;
    public IRelayCommand<string> SetFilterCommand => TodoVm.SetFilterCommand;
    public IRelayCommand ClearCompletedCommand => TodoVm.ClearCompletedCommand;

    #endregion
}

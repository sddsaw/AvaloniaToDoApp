using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaTodoApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaTodoApp.ViewModels;

public enum TodoFilter
{
    All,
    Active,
    Completed
}

/// <summary>
/// 主界面 ViewModel (类似 Vue 的 App.vue <script setup> 或 React 的主组件 Hook)
/// 负责数据管理、响应式状态以及事件处理命令 (Commands)
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // 所有任务的原始数据源
    private readonly List<TodoItemViewModel> _allTasks = new();

    // 绑定到 XAML 界面列表的响应式集合 (类似 Vue 的 computed 过滤列表)
    public ObservableCollection<TodoItemViewModel> DisplayTasks { get; } = new();

    // 双向绑定到输入框的文本 (类似 v-model="newTaskTitle")
    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    // 当前筛选模式
    [ObservableProperty]
    private TodoFilter _currentFilter = TodoFilter.All;

    // 筛选按钮高亮状态 (方便 XAML 直接绑定样式，无需写 Converter)
    [ObservableProperty]
    private bool _isFilterAll = true;

    [ObservableProperty]
    private bool _isFilterActive;

    [ObservableProperty]
    private bool _isFilterCompleted;

    // 异步加载状态守卫（防止在启动加载完成前接受用户输入造成数据覆盖）
    [ObservableProperty]
    private bool _isLoading = true;

    // 当前视图是否为空（直接用于 XAML 的 IsVisible 绑定）
    [ObservableProperty]
    private bool _isDisplayEmpty;

    // 统计计数响应式属性
    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _remainingCount;

    [ObservableProperty]
    private int _completedCount;

    [ObservableProperty]
    private bool _hasItems;

    [ObservableProperty]
    private bool _hasCompletedItems;

    [ObservableProperty]
    private string _statusSummary = string.Empty;

    [ObservableProperty]
    private string _storageStatus = "正在初始化...";

    public string AppVersion => $"v{_updateService.CurrentVersion}";

    public event Action? RequestOpenAbout;

    private readonly ITodoStorageService _storageService;
    private readonly IUpdateService _updateService;

    // 保存调度器并发锁与状态标记
    private readonly object _saveLock = new();
    private bool _isSaving;
    private bool _hasPendingSave;
    private TaskCompletionSource<bool>? _currentSaveCompletionSource;

    public MainWindowViewModel(ITodoStorageService storageService, IUpdateService updateService)
    {
        _storageService = storageService;
        _updateService = updateService;
    }

    /// <summary>
    /// 受生命周期管控的显式初始化流程
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            StorageStatus = "正在从磁盘加载数据...";
            var items = await _storageService.LoadAsync();
            _allTasks.Clear();

            foreach (var item in items)
            {
                var vm = TodoItemViewModel.FromModel(item);
                vm.StatusChanged += OnItemStatusChanged;
                _allTasks.Add(vm);
            }

            RefreshFilter();
            StorageStatus = "数据已同步";
        }
        catch (Exception ex)
        {
            StorageStatus = $"加载异常: {ex.Message}";
            Serilog.Log.Error(ex, "[VM] 初始加载待办任务失败");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 合并式自动保存调度（Coalescing Save）
    /// 无论 UI 触发多么频繁，均保证单线程顺序落盘且最后一次操作状态必定落盘
    /// </summary>
    private void TriggerAutoSave()
    {
        if (IsLoading) return;

        lock (_saveLock)
        {
            _hasPendingSave = true;
            if (_isSaving)
            {
                // 当前已有写入在进行中，稍后循环会抓取最新快照，无需启动重复任务
                return;
            }

            _isSaving = true;
            _currentSaveCompletionSource ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        _ = ProcessSaveQueueAsync();
    }

    private async Task ProcessSaveQueueAsync()
    {
        while (true)
        {
            List<Models.TodoItem> snapshot;
            lock (_saveLock)
            {
                if (!_hasPendingSave)
                {
                    _isSaving = false;
                    _currentSaveCompletionSource?.TrySetResult(true);
                    _currentSaveCompletionSource = null;
                    return;
                }

                _hasPendingSave = false;
                snapshot = _allTasks.Select(t => t.ToModel()).ToList();
            }

            try
            {
                StorageStatus = "正在自动保存...";
                await _storageService.SaveAsync(snapshot);
                StorageStatus = $"数据已自动保存 ({DateTime.Now:HH:mm:ss})";
            }
            catch (Exception ex)
            {
                StorageStatus = $"保存失败: {ex.Message}";
                Serilog.Log.Error(ex, "[VM] 任务持久化失败");
                lock (_saveLock)
                {
                    _currentSaveCompletionSource?.TrySetException(ex);
                    _currentSaveCompletionSource = null;
                    _isSaving = false;
                    _hasPendingSave = false;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 等待当前所有挂起的保存任务落盘（供窗口关闭或应用退出前调用）
    /// </summary>
    public async Task FlushAsync()
    {
        Task? waitTask = null;
        lock (_saveLock)
        {
            if (_isSaving || _hasPendingSave)
            {
                _currentSaveCompletionSource ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                waitTask = _currentSaveCompletionSource.Task;
            }
        }

        if (waitTask != null)
        {
            try
            {
                await waitTask.WaitAsync(TimeSpan.FromSeconds(3));
                Serilog.Log.Information("[VM] 退出前数据已安全 Flush 落盘");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[VM] Flush 等待保存超时或异常");
            }
        }
    }

    [RelayCommand]
    private void OpenAbout()
    {
        RequestOpenAbout?.Invoke();
    }

    /// <summary>
    /// 添加任务命令 (类似前端 @click="addTask" 或 @keydown.enter="addTask")
    /// [RelayCommand] 会自动生成 AddTaskCommand 属性供 XAML 绑定
    /// </summary>
    [RelayCommand]
    private void AddTask()
    {
        if (IsLoading || string.IsNullOrWhiteSpace(NewTaskTitle))
            return;

        var item = new TodoItemViewModel(NewTaskTitle.Trim());
        item.StatusChanged += OnItemStatusChanged;
        _allTasks.Insert(0, item); // 新任务置顶

        NewTaskTitle = string.Empty; // 清空输入框 (等同于 ref.value = '')
        RefreshFilter();
        TriggerAutoSave();
    }

    /// <summary>
    /// 删除单项任务命令
    /// </summary>
    [RelayCommand]
    private void RemoveTask(TodoItemViewModel? item)
    {
        if (IsLoading || item == null) return;

        item.StatusChanged -= OnItemStatusChanged;
        _allTasks.Remove(item);
        RefreshFilter();
        TriggerAutoSave();
    }

    /// <summary>
    /// 切换筛选条件命令
    /// </summary>
    [RelayCommand]
    private void SetFilter(string filterName)
    {
        if (Enum.TryParse<TodoFilter>(filterName, true, out var filter))
        {
            CurrentFilter = filter;
            IsFilterAll = CurrentFilter == TodoFilter.All;
            IsFilterActive = CurrentFilter == TodoFilter.Active;
            IsFilterCompleted = CurrentFilter == TodoFilter.Completed;
            RefreshFilter();
        }
    }

    /// <summary>
    /// 清除所有已完成的任务
    /// </summary>
    [RelayCommand]
    private void ClearCompleted()
    {
        if (IsLoading) return;

        var completedList = _allTasks.Where(t => t.IsCompleted).ToList();
        foreach (var item in completedList)
        {
            item.StatusChanged -= OnItemStatusChanged;
            _allTasks.Remove(item);
        }
        RefreshFilter();
        TriggerAutoSave();
    }

    private void OnItemStatusChanged()
    {
        // 状态切换时更新统计数据
        UpdateCounts();

        // 如果当前处于“待完成”或“已完成”筛选下，需动态刷新可见列表
        if (CurrentFilter != TodoFilter.All)
        {
            RefreshDisplayList();
        }

        TriggerAutoSave();
    }

    private void RefreshFilter()
    {
        UpdateCounts();
        RefreshDisplayList();
    }

    private void RefreshDisplayList()
    {
        DisplayTasks.Clear();

        IEnumerable<TodoItemViewModel> filtered = CurrentFilter switch
        {
            TodoFilter.Active => _allTasks.Where(t => !t.IsCompleted),
            TodoFilter.Completed => _allTasks.Where(t => t.IsCompleted),
            _ => _allTasks
        };

        foreach (var item in filtered)
        {
            DisplayTasks.Add(item);
        }

        IsDisplayEmpty = DisplayTasks.Count == 0;
    }

    private void UpdateCounts()
    {
        TotalCount = _allTasks.Count;
        CompletedCount = _allTasks.Count(t => t.IsCompleted);
        RemainingCount = TotalCount - CompletedCount;
        HasItems = TotalCount > 0;
        HasCompletedItems = CompletedCount > 0;

        StatusSummary = TotalCount == 0 
            ? "暂无任务" 
            : $"{RemainingCount} 项待完成 (共 {TotalCount} 项)";
    }
}

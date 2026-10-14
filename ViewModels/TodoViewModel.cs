using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvaloniaTodoApp.Models;
using AvaloniaTodoApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaTodoApp.ViewModels;

/// <summary>
/// 待办工单状态筛选枚举
/// </summary>
public enum TodoFilter
{
    All,       // 全部任务
    Active,    // 进行中（未完成）
    Completed  // 已完成
}

/// <summary>
/// 生产待办工单业务 ViewModel
/// 负责任务的增删改查、状态筛选、计数统计及高并发合并防抖自动持久化
/// </summary>
public partial class TodoViewModel : ViewModelBase
{
    private readonly ITodoStorageService _storageService;
    private readonly List<TodoItemViewModel> _allTasks = new();

    // UI 绑定的当前展示列表（根据 CurrentFilter 过滤呈现）
    public ObservableCollection<TodoItemViewModel> DisplayTasks { get; } = new();

    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    // 筛选模式（基于 NotifyPropertyChangedFor 自动驱动 UI Tab 激活态高亮）
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterAll), nameof(IsFilterActive), nameof(IsFilterCompleted))]
    private TodoFilter _currentFilter = TodoFilter.All;

    public bool IsFilterAll => CurrentFilter == TodoFilter.All;
    public bool IsFilterActive => CurrentFilter == TodoFilter.Active;
    public bool IsFilterCompleted => CurrentFilter == TodoFilter.Completed;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _isDisplayEmpty;

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

    #region 并发持久化与写盘合并锁

    private readonly object _saveLock = new();
    private bool _isSaving;
    private bool _hasPendingSave;
    private TaskCompletionSource<bool>? _currentSaveCompletionSource;

    #endregion

    public TodoViewModel(ITodoStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <summary>
    /// 异步初始化：从本地存储载入任务数据并计算初始统计
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
                vm.EditRequested += OnItemEditRequested;
                _allTasks.Add(vm);
            }

            RefreshFilter();
            StorageStatus = "数据已同步";
        }
        catch (Exception ex)
        {
            StorageStatus = $"加载异常: {ex.Message}";
            Serilog.Log.Error(ex, "[TodoVM] 初始加载待办任务失败");
        }
        finally
        {
            IsLoading = false;
        }
    }

    #region 用户交互命令 (Commands)

    /// <summary>
    /// 添加新待办任务（置顶插入并触发持久化）
    /// </summary>
    [RelayCommand]
    private void AddTask()
    {
        if (IsLoading || string.IsNullOrWhiteSpace(NewTaskTitle))
            return;

        var item = new TodoItemViewModel(NewTaskTitle.Trim());
        item.StatusChanged += OnItemStatusChanged;
        item.EditRequested += OnItemEditRequested;
        _allTasks.Insert(0, item);

        NewTaskTitle = string.Empty;
        RefreshFilter();
        TriggerAutoSave();
    }

    /// <summary>
    /// 删除指定任务项
    /// </summary>
    [RelayCommand]
    private void RemoveTask(TodoItemViewModel? item)
    {
        if (IsLoading || item == null) return;

        item.StatusChanged -= OnItemStatusChanged;
        item.EditRequested -= OnItemEditRequested;
        _allTasks.Remove(item);
        RefreshFilter();
        TriggerAutoSave();
    }

    /// <summary>
    /// 切换状态筛选 Tab
    /// </summary>
    [RelayCommand]
    private void SetFilter(string filterName)
    {
        if (Enum.TryParse<TodoFilter>(filterName, true, out var filter) && filter != CurrentFilter)
        {
            CurrentFilter = filter;
            RefreshFilter();
        }
    }

    /// <summary>
    /// 批量清空已完成的所有任务
    /// </summary>
    [RelayCommand]
    private void ClearCompleted()
    {
        if (IsLoading) return;

        var completedList = _allTasks.Where(t => t.IsCompleted).ToList();
        foreach (var item in completedList)
        {
            item.StatusChanged -= OnItemStatusChanged;
            item.EditRequested -= OnItemEditRequested;
            _allTasks.Remove(item);
        }
        RefreshFilter();
        TriggerAutoSave();
    }

    /// <summary>
    /// 上移指定任务项
    /// </summary>
    [RelayCommand]
    private void MoveTaskUp(TodoItemViewModel? item)
    {
        if (item == null || IsLoading) return;
        int idx = DisplayTasks.IndexOf(item);
        if (idx > 0)
        {
            ReorderTask(item, DisplayTasks[idx - 1], insertAfter: false);
        }
    }

    /// <summary>
    /// 下移指定任务项
    /// </summary>
    [RelayCommand]
    private void MoveTaskDown(TodoItemViewModel? item)
    {
        if (item == null || IsLoading) return;
        int idx = DisplayTasks.IndexOf(item);
        if (idx >= 0 && idx < DisplayTasks.Count - 1)
        {
            ReorderTask(item, DisplayTasks[idx + 1], insertAfter: true);
        }
    }

    /// <summary>
    /// 拖拽排序核心方法：移动任务项在全量与展示列表中的顺序并触发持久化
    /// </summary>
    /// <param name="sourceItem">被拖拽/移动的任务项</param>
    /// <param name="targetItem">目标位置参考任务项</param>
    /// <param name="insertAfter">是否插入在目标任务之后（false 则插入在目标前）</param>
    public void ReorderTask(TodoItemViewModel sourceItem, TodoItemViewModel targetItem, bool insertAfter)
    {
        if (IsLoading || sourceItem == null || targetItem == null || ReferenceEquals(sourceItem, targetItem))
            return;

        int sourceIndexInAll = _allTasks.IndexOf(sourceItem);
        int targetIndexInAll = _allTasks.IndexOf(targetItem);
        if (sourceIndexInAll < 0 || targetIndexInAll < 0)
            return;

        // 1. 调整底层全量数据列表 _allTasks
        _allTasks.RemoveAt(sourceIndexInAll);
        targetIndexInAll = _allTasks.IndexOf(targetItem);
        int newIndexInAll = insertAfter ? targetIndexInAll + 1 : targetIndexInAll;
        if (newIndexInAll < 0) newIndexInAll = 0;
        if (newIndexInAll > _allTasks.Count) newIndexInAll = _allTasks.Count;
        _allTasks.Insert(newIndexInAll, sourceItem);

        // 2. 调整当前展示列表 DisplayTasks（平滑就地调整，避免清空重建引起滚动位置跳跃）
        int sourceIndexInDisplay = DisplayTasks.IndexOf(sourceItem);
        int targetIndexInDisplay = DisplayTasks.IndexOf(targetItem);
        if (sourceIndexInDisplay >= 0 && targetIndexInDisplay >= 0)
        {
            DisplayTasks.RemoveAt(sourceIndexInDisplay);
            targetIndexInDisplay = DisplayTasks.IndexOf(targetItem);
            int newIndexInDisplay = insertAfter ? targetIndexInDisplay + 1 : targetIndexInDisplay;
            if (newIndexInDisplay < 0) newIndexInDisplay = 0;
            if (newIndexInDisplay > DisplayTasks.Count) newIndexInDisplay = DisplayTasks.Count;
            DisplayTasks.Insert(newIndexInDisplay, sourceItem);
        }
        else
        {
            RefreshDisplayList();
        }

        TriggerAutoSave();
    }

    private void OnItemEditRequested(TodoItemViewModel editingItem)
    {
        foreach (var item in _allTasks)
        {
            if (item != editingItem && item.IsEditing)
            {
                item.CancelEdit();
            }
        }
    }

    #endregion

    #region 内部数据管道与过滤统计

    private void OnItemStatusChanged()
    {
        UpdateCounts();
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

    #endregion

    #region 自动合并持久化 (Auto-Save Coalescing)

    private void TriggerAutoSave()
    {
        if (IsLoading) return;

        lock (_saveLock)
        {
            _hasPendingSave = true;
            if (_isSaving) return;

            _isSaving = true;
            _currentSaveCompletionSource ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        _ = ProcessSaveQueueAsync();
    }

    private async Task ProcessSaveQueueAsync()
    {
        while (true)
        {
            List<TodoItem> snapshot;
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
                Serilog.Log.Error(ex, "[TodoVM] 任务持久化失败");
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
    /// 程序退出前强制等待进行中的落盘完成
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
                Serilog.Log.Information("[TodoVM] 退出前数据已安全 Flush 落盘");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[TodoVM] Flush 等待保存超时或异常");
            }
        }
    }

    #endregion
}

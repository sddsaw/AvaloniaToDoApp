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
/// 待办工单与生产任务独立的 ViewModel
/// </summary>
public partial class TodoViewModel : ViewModelBase
{
    private readonly List<TodoItemViewModel> _allTasks = new();

    public ObservableCollection<TodoItemViewModel> DisplayTasks { get; } = new();

    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    [ObservableProperty]
    private TodoFilter _currentFilter = TodoFilter.All;

    [ObservableProperty]
    private bool _isFilterAll = true;

    [ObservableProperty]
    private bool _isFilterActive;

    [ObservableProperty]
    private bool _isFilterCompleted;

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

    private readonly ITodoStorageService _storageService;
    private readonly IUpdateService _updateService;

    private readonly object _saveLock = new();
    private bool _isSaving;
    private bool _hasPendingSave;
    private TaskCompletionSource<bool>? _currentSaveCompletionSource;

    public TodoViewModel(ITodoStorageService storageService, IUpdateService updateService)
    {
        _storageService = storageService;
        _updateService = updateService;
    }

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
            Serilog.Log.Error(ex, "[TodoVM] 初始加载待办任务失败");
        }
        finally
        {
            IsLoading = false;
        }
    }

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

    [RelayCommand]
    private void AddTask()
    {
        if (IsLoading || string.IsNullOrWhiteSpace(NewTaskTitle))
            return;

        var item = new TodoItemViewModel(NewTaskTitle.Trim());
        item.StatusChanged += OnItemStatusChanged;
        _allTasks.Insert(0, item);

        NewTaskTitle = string.Empty;
        RefreshFilter();
        TriggerAutoSave();
    }

    [RelayCommand]
    private void RemoveTask(TodoItemViewModel? item)
    {
        if (IsLoading || item == null) return;

        item.StatusChanged -= OnItemStatusChanged;
        _allTasks.Remove(item);
        RefreshFilter();
        TriggerAutoSave();
    }

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
}

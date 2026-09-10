using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaTodoApp.ViewModels;

/// <summary>
/// 单个待办事项的 ViewModel
/// [ObservableProperty] 类似于 Vue 3 的 ref()
/// 当属性修改时，C# 源生成器会自动生成 Title、IsCompleted 属性并自动抛出 PropertyChanged 事件通知 UI
/// </summary>
public partial class TodoItemViewModel : ViewModelBase
{
    public Guid Id { get; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isCompleted;

    public DateTimeOffset CreatedAt { get; }

    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.CurrentCulture);

    // 状态变化回调，用于通知主列表刷新统计与过滤
    public event Action? StatusChanged;

    public TodoItemViewModel(string title, bool isCompleted = false)
        : this(Guid.NewGuid(), title, isCompleted, DateTimeOffset.Now)
    {
    }

    public TodoItemViewModel(Guid id, string title, bool isCompleted, DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        IsCompleted = isCompleted;
        CreatedAt = createdAt;
    }

    public static TodoItemViewModel FromModel(Models.TodoItem item)
    {
        return new TodoItemViewModel(item.Id, item.Title, item.IsCompleted, item.CreatedAt);
    }

    public Models.TodoItem ToModel()
    {
        return new Models.TodoItem
        {
            Id = Id,
            Title = Title,
            IsCompleted = IsCompleted,
            CreatedAt = CreatedAt
        };
    }

    partial void OnIsCompletedChanged(bool value)
    {
        StatusChanged?.Invoke();
    }
}

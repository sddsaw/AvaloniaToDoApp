using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaTodoApp.ViewModels;

/// <summary>
/// 单个待办事项的 ViewModel
/// [ObservableProperty] 类似于 Vue 3 的 ref()
/// 当属性修改时，C# 源生成器会自动生成 Title、IsCompleted 等属性并自动抛出 PropertyChanged 事件通知 UI
/// </summary>
public partial class TodoItemViewModel : ViewModelBase
{
    public Guid Id { get; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editText = string.Empty;

    public DateTimeOffset CreatedAt { get; }

    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.CurrentCulture);

    // 状态或文案变化回调，用于通知主列表刷新统计与自动持久化
    public event Action? StatusChanged;

    // 当请求进入编辑态时通知，用于让主视图关闭其他条目的编辑状态
    public event Action<TodoItemViewModel>? EditRequested;

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

    /// <summary>
    /// 进入文案修改编辑模式
    /// </summary>
    [RelayCommand]
    public void StartEdit()
    {
        EditText = Title;
        IsEditing = true;
        EditRequested?.Invoke(this);
    }

    /// <summary>
    /// 确认提交文案修改
    /// </summary>
    [RelayCommand]
    public void CommitEdit()
    {
        if (!IsEditing) return;

        var trimmed = EditText?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            if (trimmed != Title)
            {
                Title = trimmed;
            }
        }
        else
        {
            // 若用户输入为空白，则还原为修改前的标题
            EditText = Title;
        }

        IsEditing = false;
    }

    /// <summary>
    /// 取消文案修改
    /// </summary>
    [RelayCommand]
    public void CancelEdit()
    {
        EditText = Title;
        IsEditing = false;
    }

    partial void OnTitleChanged(string value)
    {
        StatusChanged?.Invoke();
    }

    partial void OnIsCompletedChanged(bool value)
    {
        StatusChanged?.Invoke();
    }
}

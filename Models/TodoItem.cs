using System;

namespace AvaloniaTodoApp.Models;

/// <summary>
/// 纯数据模型（类似 TypeScript 中的 interface TodoItem）
/// </summary>
public class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

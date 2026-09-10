using System.Collections.Generic;
using System.Threading.Tasks;
using AvaloniaTodoApp.Models;

namespace AvaloniaTodoApp.Services;

/// <summary>
/// 本地数据持久化接口
/// </summary>
public interface ITodoStorageService
{
    /// <summary>
    /// 加载所有待办事项
    /// </summary>
    Task<List<TodoItem>> LoadAsync();

    /// <summary>
    /// 保存待办事项列表到磁盘
    /// </summary>
    Task SaveAsync(IEnumerable<TodoItem> items);

    /// <summary>
    /// 获取当前存储文件路径（方便调试与关于界面展示）
    /// </summary>
    string StorageFilePath { get; }
}

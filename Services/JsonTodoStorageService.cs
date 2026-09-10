using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaTodoApp.Models;

namespace AvaloniaTodoApp.Services;

/// <summary>
/// 基于本地 JSON 文件的持久化实现
/// 自动保存在系统 AppData / Application Support 目录下
/// </summary>
public class JsonTodoStorageService : ITodoStorageService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public string StorageFilePath => _filePath;

    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public JsonTodoStorageService(string? customFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            _filePath = customFilePath;
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "AvaloniaTodoApp");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _filePath = Path.Combine(dir, "todos.json");
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<TodoItem>> LoadAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_filePath))
            {
                // 首次使用，提供贴心的新手引导预设数据
                var initialItems = new List<TodoItem>
                {
                    new() { Title = "了解 Avalonia 跨平台核心架构与 XAML 语法", IsCompleted = true, CreatedAt = DateTimeOffset.Now.AddHours(-2) },
                    new() { Title = "体验系统托盘、自动持久化与版本检测基础建设", IsCompleted = false, CreatedAt = DateTimeOffset.Now.AddHours(-1) },
                    new() { Title = "试一试：关闭窗口自动隐藏并常驻系统托盘", IsCompleted = false, CreatedAt = DateTimeOffset.Now }
                };
                await SaveInternalAsync(initialItems);
                return initialItems;
            }

            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                var items = JsonSerializer.Deserialize<List<TodoItem>>(json, _jsonOptions);
                var result = items ?? new List<TodoItem>();
                Serilog.Log.Information("[Storage] 成功从磁盘加载 {Count} 项任务，路径: {Path}", result.Count, _filePath);
                return result;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[Storage] 读取 todos.json 失败或文件损坏: {Path}", _filePath);

                // 损坏备份保护：避免损坏文件被直接覆盖丢失
                try
                {
                    var corruptBackup = _filePath + $".corrupted.{DateTime.Now:yyyyMMddHHmmss}";
                    File.Copy(_filePath, corruptBackup, overwrite: true);
                    Serilog.Log.Warning("[Storage] 已将损坏的数据文件备份至: {BackupPath}", corruptBackup);
                }
                catch (Exception copyEx)
                {
                    Serilog.Log.Error(copyEx, "[Storage] 备份损坏文件失败");
                }

                throw new InvalidOperationException($"待办数据文件已损坏，系统已自动保留备份。详细错误: {ex.Message}", ex);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveAsync(IEnumerable<TodoItem> items)
    {
        await _fileLock.WaitAsync();
        try
        {
            await SaveInternalAsync(items);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveInternalAsync(IEnumerable<TodoItem> items)
    {
        var list = items as IList<TodoItem> ?? items.ToList();
        var json = JsonSerializer.Serialize(list, _jsonOptions);
        var tempFilePath = _filePath + ".tmp";

        try
        {
            await File.WriteAllTextAsync(tempFilePath, json);
            File.Move(tempFilePath, _filePath, overwrite: true);
            Serilog.Log.Information("[Storage] 成功原子持久化 {Count} 项任务", list.Count);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[Storage] 写入 todos.json 失败: {Path}", _filePath);
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { /* 忽略临时文件删除异常 */ }
            }
            throw;
        }
    }
}

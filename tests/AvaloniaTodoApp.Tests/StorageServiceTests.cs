using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using AvaloniaTodoApp.Models;
using AvaloniaTodoApp.Services;

namespace AvaloniaTodoApp.Tests;

public class StorageServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testFile;

    public StorageServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "AvaloniaTodoApp_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _testFile = Path.Combine(_testDir, "todos.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
            // 忽略临时测试目录清理异常
        }
    }

    [Fact]
    public async Task LoadAsync_FileNotExist_ShouldCreateInitialSeedData()
    {
        var service = new JsonTodoStorageService(_testFile);
        var items = await service.LoadAsync();

        Assert.NotEmpty(items);
        Assert.True(File.Exists(_testFile));
    }

    [Fact]
    public async Task SaveAsync_And_LoadAsync_ShouldPreserveDataAccurately()
    {
        var service = new JsonTodoStorageService(_testFile);
        var original = new List<TodoItem>
        {
            new() { Title = "Task 1", IsCompleted = true, CreatedAt = DateTimeOffset.Now.AddHours(-1) },
            new() { Title = "Task 2", IsCompleted = false, CreatedAt = DateTimeOffset.Now }
        };

        await service.SaveAsync(original);
        var loaded = await service.LoadAsync();

        Assert.Equal(2, loaded.Count);
        Assert.Equal("Task 1", loaded[0].Title);
        Assert.True(loaded[0].IsCompleted);
        Assert.Equal("Task 2", loaded[1].Title);
        Assert.False(loaded[1].IsCompleted);
    }

    [Fact]
    public async Task SaveAsync_HighConcurrency_ShouldBeThreadSafeWithoutLockConflict()
    {
        var service = new JsonTodoStorageService(_testFile);

        var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(async () =>
        {
            var list = new List<TodoItem>
            {
                new() { Title = $"Concurrent Task {i}", IsCompleted = i % 2 == 0 }
            };
            await service.SaveAsync(list);
        }));

        await Task.WhenAll(tasks);

        // 验证没有残留的 .tmp 临时文件
        var tmpFile = _testFile + ".tmp";
        Assert.False(File.Exists(tmpFile), "临时文件未被清理");

        // 验证文件内容是合法 JSON
        var loaded = await service.LoadAsync();
        Assert.Single(loaded);
    }

    [Fact]
    public async Task LoadAsync_CorruptedJson_ShouldBackupAndThrowException()
    {
        var service = new JsonTodoStorageService(_testFile);
        await File.WriteAllTextAsync(_testFile, "{\"invalid_json\": [");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoadAsync());
        Assert.Contains("待办数据文件已损坏", ex.Message);

        var corruptFiles = Directory.GetFiles(_testDir, "todos.json.corrupted.*");
        Assert.NotEmpty(corruptFiles);
    }
}

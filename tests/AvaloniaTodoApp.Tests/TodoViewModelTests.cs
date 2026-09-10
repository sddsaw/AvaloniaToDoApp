using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using AvaloniaTodoApp.Models;
using AvaloniaTodoApp.Services;
using AvaloniaTodoApp.ViewModels;

namespace AvaloniaTodoApp.Tests;

public class TodoViewModelTests
{
    private class FakeStorageService : ITodoStorageService
    {
        public List<TodoItem> SavedItems { get; set; } = new();
        public string StorageFilePath => "/fake/path/todos.json";

        public Task<List<TodoItem>> LoadAsync()
        {
            return Task.FromResult(new List<TodoItem>
            {
                new() { Title = "Initial 1", IsCompleted = false },
                new() { Title = "Initial 2", IsCompleted = true }
            });
        }

        public Task SaveAsync(IEnumerable<TodoItem> items)
        {
            SavedItems = new List<TodoItem>(items);
            return Task.CompletedTask;
        }
    }

    private class FakeUpdateService : IUpdateService
    {
        public string CurrentVersion => "1.0.0";

        public Task<UpdateInfo> CheckForUpdatesAsync()
        {
            return Task.FromResult(new UpdateInfo
            {
                CurrentVersion = "1.0.0",
                LatestVersion = "1.0.0",
                HasUpdate = false
            });
        }
    }

    [Fact]
    public async Task InitializeAsync_ShouldLoadTasksAndCalculateStats()
    {
        var storage = new FakeStorageService();
        var update = new FakeUpdateService();
        var vm = new MainWindowViewModel(storage, update);

        await vm.InitializeAsync();

        Assert.False(vm.IsLoading);
        Assert.Equal(2, vm.TotalCount);
        Assert.Equal(1, vm.RemainingCount);
        Assert.Equal(1, vm.CompletedCount);
        Assert.Equal(2, vm.DisplayTasks.Count);
    }

    [Fact]
    public async Task AddTaskCommand_ShouldInsertNewTaskAndRefreshDisplay()
    {
        var storage = new FakeStorageService();
        var update = new FakeUpdateService();
        var vm = new MainWindowViewModel(storage, update);
        await vm.InitializeAsync();

        vm.NewTaskTitle = "Write Unit Tests";
        vm.AddTaskCommand.Execute(null);

        Assert.Equal(3, vm.TotalCount);
        Assert.Equal(2, vm.RemainingCount);
        Assert.Equal("Write Unit Tests", vm.DisplayTasks[0].Title);
        Assert.Empty(vm.NewTaskTitle);
    }

    [Fact]
    public async Task FilterCommand_ShouldFilterTasksByStatus()
    {
        var storage = new FakeStorageService();
        var update = new FakeUpdateService();
        var vm = new MainWindowViewModel(storage, update);
        await vm.InitializeAsync();

        // 切换至 Active (未完成)
        vm.SetFilterCommand.Execute("Active");
        Assert.Equal(TodoFilter.Active, vm.CurrentFilter);
        Assert.Single(vm.DisplayTasks);
        Assert.False(vm.DisplayTasks[0].IsCompleted);

        // 切换至 Completed (已完成)
        vm.SetFilterCommand.Execute("Completed");
        Assert.Equal(TodoFilter.Completed, vm.CurrentFilter);
        Assert.Single(vm.DisplayTasks);
        Assert.True(vm.DisplayTasks[0].IsCompleted);

        // 切换回 All
        vm.SetFilterCommand.Execute("All");
        Assert.Equal(2, vm.DisplayTasks.Count);
    }

    [Fact]
    public async Task ClearCompletedCommand_ShouldRemoveAllCompletedTasks()
    {
        var storage = new FakeStorageService();
        var update = new FakeUpdateService();
        var vm = new MainWindowViewModel(storage, update);
        await vm.InitializeAsync();

        vm.ClearCompletedCommand.Execute(null);

        Assert.Equal(1, vm.TotalCount);
        Assert.Equal(1, vm.RemainingCount);
        Assert.Equal(0, vm.CompletedCount);
        Assert.Single(vm.DisplayTasks);
        Assert.False(vm.DisplayTasks[0].IsCompleted);
    }
}

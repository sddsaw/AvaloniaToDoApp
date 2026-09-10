using System.Threading.Tasks;
using Xunit;
using AvaloniaTodoApp.Services;
using AvaloniaTodoApp.ViewModels;

namespace AvaloniaTodoApp.Tests;

/// <summary>
/// 主界面路由外壳 ViewModel 自动化测试
/// 验证多模块路由切换与状态联动机制
/// </summary>
public class MainWindowViewModelTests
{
    private class FakeUpdateService : IUpdateService
    {
        public string CurrentVersion => "1.0.0";
        public Task<UpdateInfo> CheckForUpdatesAsync() => Task.FromResult(new UpdateInfo());
    }

    private class FakeStorageService : ITodoStorageService
    {
        public string StorageFilePath => "/fake/todos.json";
        public Task<System.Collections.Generic.List<Models.TodoItem>> LoadAsync() => Task.FromResult(new System.Collections.Generic.List<Models.TodoItem>());
        public Task SaveAsync(System.Collections.Generic.IEnumerable<Models.TodoItem> items) => Task.CompletedTask;
    }

    [Fact]
    public void SwitchModule_ShouldUpdateCurrentPageAndFlags()
    {
        var chipVm = new ChipInspectViewModel();
        var todoVm = new TodoViewModel(new FakeStorageService());
        var logsVm = new DeviceLogsViewModel();
        var update = new FakeUpdateService();

        var shell = new MainWindowViewModel(chipVm, todoVm, logsVm, update);

        // 默认应当为 3D 芯片检测看板
        Assert.Equal(NavModule.ChipInspect, shell.CurrentModule);
        Assert.Same(chipVm, shell.CurrentPage);
        Assert.True(shell.IsModuleChipInspect);
        Assert.False(shell.IsModuleTodo);
        Assert.False(shell.IsModuleLogs);

        // 切换到待办工单模块
        shell.SwitchModuleCommand.Execute("Todo");
        Assert.Equal(NavModule.Todo, shell.CurrentModule);
        Assert.Same(todoVm, shell.CurrentPage);
        Assert.False(shell.IsModuleChipInspect);
        Assert.True(shell.IsModuleTodo);
        Assert.False(shell.IsModuleLogs);

        // 切换到工业日志模块
        shell.SwitchModuleCommand.Execute("Logs");
        Assert.Equal(NavModule.Logs, shell.CurrentModule);
        Assert.Same(logsVm, shell.CurrentPage);
        Assert.False(shell.IsModuleChipInspect);
        Assert.False(shell.IsModuleTodo);
        Assert.True(shell.IsModuleLogs);
    }
}

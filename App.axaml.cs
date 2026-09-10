using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaTodoApp.Services;
using AvaloniaTodoApp.ViewModels;
using AvaloniaTodoApp.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaTodoApp;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static App Instance => (App)Current!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 1. 初始化依赖注入容器 (IoC / DI)
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // 2. 初始化经典桌面生命周期与主窗口
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow
            {
                DataContext = mainVm
            };

            mainVm.RequestOpenAbout += ShowAboutWindow;

            desktop.MainWindow = mainWindow;

            // 显式受控启动初始化
            _ = mainVm.InitializeAsync();

            // 3. 应用程序彻底退出时安全释放 DI 容器资源
            desktop.Exit += (sender, args) =>
            {
                if (Services is IDisposable disposable)
                {
                    disposable.Dispose();
                    Serilog.Log.Information("[Lifecycle] DI 服务提供器资源已安全释放");
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 注册核心基础设施服务 (单例模式)
        services.AddSingleton<ITodoStorageService, JsonTodoStorageService>();
        services.AddSingleton<IUpdateService, UpdateService>();

        // 注册 ViewModels (瞬态模式)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<AboutUpdateViewModel>();
    }

    #region 系统托盘事件处理 (TrayIcon Events)

    private void TrayIcon_OnClicked(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void MenuShowMainWindow_OnClick(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void MenuShowAbout_OnClick(object? sender, EventArgs e)
    {
        ShowAboutWindow();
    }

    private void MenuExit_OnClick(object? sender, EventArgs e)
    {
        ExitApp();
    }

    public void ShowMainWindow()
    {
        Serilog.Log.Information("[UI] 用户唤醒并激活主窗口");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            if (!desktop.MainWindow.IsVisible)
            {
                desktop.MainWindow.Show();
            }
            if (desktop.MainWindow.WindowState == WindowState.Minimized)
            {
                desktop.MainWindow.WindowState = WindowState.Normal;
            }
            desktop.MainWindow.Activate();
        }
    }

    public void ShowAboutWindow()
    {
        Serilog.Log.Information("[UI] 打开关于与版本更新窗口");
        var aboutVm = Services.GetRequiredService<AboutUpdateViewModel>();
        var aboutWindow = new AboutUpdateWindow
        {
            DataContext = aboutVm
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is { IsVisible: true })
        {
            aboutWindow.ShowDialog(desktop.MainWindow);
        }
        else
        {
            aboutWindow.Show();
        }
    }

    public async void ExitApp()
    {
        Serilog.Log.Information("[Lifecycle] 接收到退出程序指令，执行安全关闭");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is MainWindow mainWin)
            {
                mainWin.CanClose = true;
                if (mainWin.DataContext is MainWindowViewModel vm)
                {
                    await vm.FlushAsync();
                }
            }
            desktop.Shutdown();
        }
    }

    #endregion
}

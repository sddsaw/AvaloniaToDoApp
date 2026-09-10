using System;
using System.Threading.Tasks;
using Avalonia;
using AvaloniaTodoApp.Services;
using Serilog;

namespace AvaloniaTodoApp;

class Program
{
    // 应用程序启动入口
    [STAThread]
    public static void Main(string[] args)
    {
        // 1. 注册全局未捕获异常防崩溃兜底（最优先执行，拦截全生命周期异常）
        SetupGlobalExceptionHandling();

        // 2. 初始化 Serilog 工业级日志底座
        LogHelper.InitLogger();

        try
        {
            Log.Information(">>> AvaloniaTodoApp 应用程序正在启动 <<<");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, ">>> 应用程序遭遇致命未捕获异常，主循环终止 <<<");
        }
        finally
        {
            Log.Information(">>> AvaloniaTodoApp 应用程序已安全退出，刷新日志缓冲区 <<<");
            Log.CloseAndFlush();
        }
    }

    private static void SetupGlobalExceptionHandling()
    {
        // 拦截 AppDomain 未处理异常
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "[GlobalCrashDefense] AppDomain 捕获到未处理异常，进程是否将终止: {IsTerminating}", e.IsTerminating);
            }
            else
            {
                Log.Fatal("[GlobalCrashDefense] AppDomain 捕获到非 Exception 对象: {Obj}", e.ExceptionObject);
            }
        };

        // 拦截 Task 后台未观察到的异常 (防止后台线程漏写 await 或后台任务抛错导致进程异常)
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Log.Error(e.Exception, "[GlobalCrashDefense] TaskScheduler 捕获到后台任务未观察异常");
            // 标记已观察，防止某些环境下引发进程强退
            e.SetObserved();
        };
    }

    // Avalonia 运行时与图形引擎初始化
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Events;

namespace AvaloniaTodoApp.Services;

/// <summary>
/// 工业级日志管理工具
/// 统一配置 Serilog 控制台与本地按天滚动文件写入
/// </summary>
public static class LogHelper
{
    private static string _logDir = string.Empty;

    public static string LogDirectoryPath => _logDir;

    /// <summary>
    /// 初始化全局 Serilog 记录器，具备安全降级保护
    /// </summary>
    public static void InitLogger()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _logDir = Path.Combine(appData, "AvaloniaTodoApp", "logs");
            if (!Directory.Exists(_logDir))
            {
                Directory.CreateDirectory(_logDir);
            }
        }
        catch (Exception ex)
        {
            try
            {
                _logDir = Path.Combine(Path.GetTempPath(), "AvaloniaTodoApp", "logs");
                if (!Directory.Exists(_logDir))
                {
                    Directory.CreateDirectory(_logDir);
                }
                Console.Error.WriteLine($"[LogHelper] 默认日志目录初始化失败，已安全降级至临时目录: {_logDir}. 原因: {ex.Message}");
            }
            catch (Exception fallbackEx)
            {
                _logDir = Path.GetTempPath();
                Console.Error.WriteLine($"[LogHelper] 降级目录创建失败，直接使用临时根目录: {fallbackEx.Message}");
            }
        }

        var logFilePathTemplate = Path.Combine(_logDir, "app-.log");
        const string outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        var config = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Avalonia", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            // 1. 控制台输出 (本地调试实时查看)
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: outputTemplate);

        try
        {
            // 2. 本地文件输出 (按天滚动，毫秒级时间戳，保留 30 天，单文件上限 10MB)
            config.WriteTo.File(
                path: logFilePathTemplate,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: outputTemplate,
                restrictedToMinimumLevel: LogEventLevel.Information);
        }
        catch (Exception fileEx)
        {
            Console.Error.WriteLine($"[LogHelper] 配置文件输出失败，仅保留控制台输出: {fileEx.Message}");
        }

        Log.Logger = config.CreateLogger();

        Log.Information("=================================================");
        Log.Information("=== 工业日志基础设施已就绪，当前日志目录: {LogDir} ===", _logDir);
        Log.Information("=================================================");
    }

    /// <summary>
    /// 跨平台调起系统文件管理器打开日志目录
    /// </summary>
    public static void OpenLogFolder()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_logDir))
            {
                return;
            }

            if (!Directory.Exists(_logDir))
            {
                Directory.CreateDirectory(_logDir);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var psi = new ProcessStartInfo("open")
                {
                    UseShellExecute = false
                };
                psi.ArgumentList.Add(_logDir);
                Process.Start(psi);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows 下直接以 ShellExecute 启动目录路径，操作系统会自动以资源管理器打开，天然兼容空格与中文字符
                Process.Start(new ProcessStartInfo
                {
                    FileName = _logDir,
                    UseShellExecute = true
                });
            }
            else
            {
                var psi = new ProcessStartInfo("xdg-open")
                {
                    UseShellExecute = false
                };
                psi.ArgumentList.Add(_logDir);
                Process.Start(psi);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开日志目录失败: {Path}", _logDir);
        }
    }
}

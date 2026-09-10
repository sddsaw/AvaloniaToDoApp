using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;

namespace AvaloniaTodoApp.Views;

public partial class ChipInspectView : UserControl
{
    private bool _isLoaded;

    public ChipInspectView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            _isLoaded = true;
            LoadHtmlContent();
        }
    }

    private void BtnReload_OnClick(object? sender, RoutedEventArgs e)
    {
        LoadHtmlContent();
    }

    private void LoadHtmlContent()
    {
        try
        {
            var uri = new Uri("avares://AvaloniaTodoApp/Assets/web/chip_dashboard.html");
            if (AssetLoader.Exists(uri))
            {
                using var stream = AssetLoader.Open(uri);
                using var reader = new StreamReader(stream);
                var html = reader.ReadToEnd();
                InspectWebView.HtmlContent = html;
                Serilog.Log.Information("[WebView] 成功加载 3D 芯片检测看板 HTML (字符数: {Len})", html.Length);
            }
            else
            {
                Serilog.Log.Warning("[WebView] 找不到内置看板资源: {Uri}", uri);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[WebView] 加载 3D 芯片检测看板失败");
        }
    }
}

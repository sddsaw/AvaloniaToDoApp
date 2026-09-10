using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using AvaloniaTodoApp.ViewModels;

namespace AvaloniaTodoApp;

/// <summary>
/// 【前端对比】ViewLocator 相当于桌面端的 Vue Router 路由定位器与组件解析器
/// 负责根据 ViewModel 类型（例如 ChipInspectViewModel）自动反射解析并构建对应的 View（ChipInspectView）
/// 并在内部集成类似 Vue <keep-alive> 的视图实例缓存机制，防止切出时重复销毁 WebGL 3D 画布与输入状态。
/// </summary>
public class ViewLocator : IDataTemplate
{
    // 视图实例缓存字典（等价于 Vue 的 keep-alive 页面缓存）
    private readonly Dictionary<object, Control> _viewCache = new();

    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        // 优先从缓存获取已渲染的视图，避免 Three.js WebGL 上下文反复销毁重建
        if (_viewCache.TryGetValue(data, out var cachedView))
        {
            return cachedView;
        }

        // 约定优于配置：将 ViewModel 后缀替换为 View 查找对应的 UI 组件
        var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            var view = (Control)Activator.CreateInstance(type)!;
            view.DataContext = data;
            _viewCache[data] = view;
            return view;
        }

        return new TextBlock
        {
            Text = $"[ViewLocator] 未找到对应的路由视图组件: {name}",
            Margin = new Avalonia.Thickness(20)
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}

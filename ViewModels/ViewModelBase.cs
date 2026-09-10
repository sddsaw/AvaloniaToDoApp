using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaTodoApp.ViewModels;

/// <summary>
/// ViewModel 基类
/// 继承 CommunityToolkit.Mvvm 的 ObservableObject，自动获得响应式属性变更通知机制
/// 类似于 Vue 的响应式追踪系统（Reactivity System）
/// </summary>
public class ViewModelBase : ObservableObject
{
}

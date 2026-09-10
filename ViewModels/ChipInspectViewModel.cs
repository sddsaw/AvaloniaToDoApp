using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaTodoApp.ViewModels;

/// <summary>
/// 3D 芯片全业务与上位机监控看板 ViewModel
/// </summary>
public partial class ChipInspectViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "半导体芯片全业务 3D 监控与上位机看板";

    [ObservableProperty]
    private bool _isPlcConnected = true;

    [ObservableProperty]
    private string _engineInfo = "Three.js WebGL 硬件直绘引擎";
}

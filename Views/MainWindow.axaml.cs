using Avalonia.Controls;

namespace AvaloniaTodoApp.Views;

public partial class MainWindow : Window
{
    /// <summary>
    /// 是否允许真正关闭窗口。当为 false 时，点击关闭按钮仅隐藏到系统托盘
    /// </summary>
    public bool CanClose { get; set; } = false;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // 如果不是通过托盘菜单或应用程序退出指令触发的关闭，则拦截关闭事件，隐藏窗口并保持托盘驻留
        if (!CanClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        if (DataContext is ViewModels.MainWindowViewModel vm)
        {
            vm.FlushAsync().GetAwaiter().GetResult();
        }

        base.OnClosing(e);
    }
}

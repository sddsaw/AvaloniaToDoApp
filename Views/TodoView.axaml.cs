using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaTodoApp.ViewModels;

namespace AvaloniaTodoApp.Views;

/// <summary>
/// 待办工单与生产任务主视图
/// 集成桌面级流畅拖拽重排序、行内文案即时修改、键盘快捷键与视觉高亮反馈
/// </summary>
public partial class TodoView : UserControl
{
    private const string TodoItemDataFormat = "application/x-todo-item";

    private Point? _dragStartPoint;
    private TodoItemViewModel? _draggedItem;
    private Border? _draggedBorder;
    private bool _isDragHandlePressed;

    // 当前处于拖拽生命周期中的数据项（内存直通，杜绝任何剪贴板序列化异常）
    private TodoItemViewModel? _activeDragItem;
    private Border? _currentDropTargetBorder;

    public TodoView()
    {
        InitializeComponent();

        // 注册拖放生命周期事件 (Drag & Drop Routed Events)
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);

        // 注册指针事件用于平滑拖拽拾取识别 (Tunnel 隧道策略保障手柄与卡片可被精准监听)
        AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel);
    }

    #region 拖拽指针手势捕获 (Pointer & Drag Initiation)

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        // 若点击在复选框、按钮或文本输入框内部，不触发拖拽逻辑，保证原生点击绝对流畅
        if (IsInteractiveControl(e.Source as Visual))
        {
            _dragStartPoint = null;
            _draggedItem = null;
            _draggedBorder = null;
            return;
        }

        var border = FindTaskItemBorder(e.Source);
        if (border?.DataContext is TodoItemViewModel item)
        {
            _dragStartPoint = e.GetPosition(this);
            _draggedItem = item;
            _draggedBorder = border;

            // 检查是否从 6 点拖拽手柄按下
            _isDragHandlePressed = IsDragHandle(e.Source as Visual);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragStartPoint.HasValue || _draggedItem == null)
            return;

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = null;
            _draggedItem = null;
            _draggedBorder = null;
            return;
        }

        var currentPos = e.GetPosition(this);
        var diff = currentPos - _dragStartPoint.Value;

        // 若手柄按下，阈值设为 2px 极速启动；若卡片空白处按下，阈值设为 6px 避免误触
        double threshold = _isDragHandlePressed ? 2.0 : 6.0;
        if (Math.Abs(diff.X) > threshold || Math.Abs(diff.Y) > threshold)
        {
            StartDrag(e);
        }
    }

    private async void StartDrag(PointerEventArgs e)
    {
        var item = _draggedItem;
        var border = _draggedBorder;
        _dragStartPoint = null;

        if (item == null) return;
        _activeDragItem = item;

        if (border != null)
        {
            border.Classes.Add("is-dragging");
        }

        var data = new DataObject();
        data.Set(TodoItemDataFormat, item);

        try
        {
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[TodoView] 拖拽排序异常中断");
        }
        finally
        {
            if (border != null)
            {
                border.Classes.Remove("is-dragging");
            }
            ClearAllDropIndicators();
            _draggedItem = null;
            _draggedBorder = null;
            _activeDragItem = null;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragStartPoint = null;
        _draggedItem = null;
        _draggedBorder = null;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _dragStartPoint = null;
        _draggedItem = null;
        _draggedBorder = null;
    }

    #endregion

    #region 拖放悬浮与落地处理 (DragOver & Drop Handling)

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var sourceItem = _activeDragItem ?? (e.Data.Get(TodoItemDataFormat) as TodoItemViewModel);
        if (sourceItem == null)
        {
            e.DragEffects = DragDropEffects.None;
            ClearAllDropIndicators();
            return;
        }

        var targetBorder = FindTaskItemBorder(e.Source);
        if (targetBorder?.DataContext is TodoItemViewModel targetItem)
        {
            if (ReferenceEquals(sourceItem, targetItem))
            {
                e.DragEffects = DragDropEffects.None;
                ClearAllDropIndicators();
                return;
            }

            e.DragEffects = DragDropEffects.Move;
            var pos = e.GetPosition(targetBorder);
            bool isTopHalf = pos.Y < targetBorder.Bounds.Height / 2.0;
            SetDropIndicator(targetBorder, isTopHalf);
        }
        else
        {
            // 悬停在列表空白或边缘区域
            e.DragEffects = DragDropEffects.Move;
            ClearAllDropIndicators();
        }

        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        var targetBorder = FindTaskItemBorder(e.Source);
        if (targetBorder != null && targetBorder == _currentDropTargetBorder)
        {
            ClearAllDropIndicators();
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var sourceItem = _activeDragItem ?? (e.Data.Get(TodoItemDataFormat) as TodoItemViewModel);
        var targetBorder = FindTaskItemBorder(e.Source);

        if (sourceItem != null && DataContext is TodoViewModel vm)
        {
            if (targetBorder?.DataContext is TodoItemViewModel targetItem && !ReferenceEquals(sourceItem, targetItem))
            {
                var pos = e.GetPosition(targetBorder);
                bool insertAfter = pos.Y >= targetBorder.Bounds.Height / 2.0;
                vm.ReorderTask(sourceItem, targetItem, insertAfter);
            }
            else if (targetBorder == null && vm.DisplayTasks.Count > 0)
            {
                // 拖拽到列表底部空白区域时，默认追加到当前列表末尾
                var lastItem = vm.DisplayTasks.Last();
                if (!ReferenceEquals(sourceItem, lastItem))
                {
                    vm.ReorderTask(sourceItem, lastItem, insertAfter: true);
                }
            }
        }

        ClearAllDropIndicators();
        _draggedItem = null;
        _draggedBorder = null;
        _activeDragItem = null;
        e.Handled = true;
    }

    private void SetDropIndicator(Border border, bool isTopHalf)
    {
        if (_currentDropTargetBorder != null && _currentDropTargetBorder != border)
        {
            _currentDropTargetBorder.Classes.Remove("drag-over-top");
            _currentDropTargetBorder.Classes.Remove("drag-over-bottom");
        }

        _currentDropTargetBorder = border;

        if (isTopHalf)
        {
            border.Classes.Remove("drag-over-bottom");
            if (!border.Classes.Contains("drag-over-top"))
            {
                border.Classes.Add("drag-over-top");
            }
        }
        else
        {
            border.Classes.Remove("drag-over-top");
            if (!border.Classes.Contains("drag-over-bottom"))
            {
                border.Classes.Add("drag-over-bottom");
            }
        }
    }

    private void ClearAllDropIndicators()
    {
        if (_currentDropTargetBorder != null)
        {
            _currentDropTargetBorder.Classes.Remove("drag-over-top");
            _currentDropTargetBorder.Classes.Remove("drag-over-bottom");
            _currentDropTargetBorder = null;
        }
    }

    #endregion

    #region 文案修改与交互逻辑 (Inline Editing)

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (IsInteractiveControl(e.Source as Visual))
            return;

        var border = FindTaskItemBorder(e.Source);
        if (border?.DataContext is TodoItemViewModel item)
        {
            item.StartEdit();
        }
    }

    /// <summary>
    /// 当行内编辑框加载或显示状态切换时，自动聚焦并全选现有文案
    /// </summary>
    private void EditTextBox_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.PropertyChanged += (s, args) =>
            {
                if (args.Property == Visual.IsVisibleProperty && args.NewValue is true)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        tb.Focus();
                        tb.SelectAll();
                    }, DispatcherPriority.Input);
                }
            };

            if (tb.IsVisible)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    tb.Focus();
                    tb.SelectAll();
                }, DispatcherPriority.Input);
            }
        }
    }

    #endregion

    #region 视觉树辅助方法 (Visual Tree Helpers)

    private static Border? FindTaskItemBorder(object? source)
    {
        var current = source as Visual;
        while (current != null)
        {
            if (current is Border border && border.Classes.Contains("task-item"))
            {
                return border;
            }
            current = current.GetVisualParent();
        }
        return null;
    }

    private static bool IsDragHandle(Visual? visual)
    {
        var current = visual;
        while (current != null)
        {
            if (current is Border b && b.Classes.Contains("drag-handle"))
            {
                return true;
            }
            current = current.GetVisualParent();
        }
        return false;
    }

    private static bool IsInteractiveControl(Visual? visual)
    {
        var current = visual;
        while (current != null)
        {
            if (current is Button || current is CheckBox || current is TextBox)
            {
                return true;
            }
            current = current.GetVisualParent();
        }
        return false;
    }

    #endregion
}

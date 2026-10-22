using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KanbanCards.Models;
using KanbanCards.Services;
using KanbanCards.ViewModels;

namespace KanbanCards.Views;

public partial class EdgeStackWindow : Window
{
    private EdgeStackViewModel _viewModel;
    private readonly IEditorService _editorService;
    private readonly ISettingsService _settingsService;
    private bool _isHovering;

    public EdgeStackWindow(EdgeStackViewModel viewModel, IEditorService editorService, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _editorService = editorService;
        _settingsService = settingsService;
        DataContext = _viewModel;

        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        Dispatcher.Invoke(() =>
        {
            var settings = _settingsService.LoadSettings();
            if (settings.ShowNotesOnDesktop)
            {
                ApplyWindowPosition(settings.StackPosition);
                _viewModel.ApplySettings(settings.StackPosition);
                UpdateExpandedState(_viewModel.IsExpanded);
                this.Show();
            }
            else
            {
                this.Hide();
            }
        });
    }

    private void ApplyWindowPosition(string stackPosition)
    {
        if (stackPosition == "Left")
        {
            this.Left = SystemParameters.VirtualScreenLeft;
        }
        else
        {
            this.Left = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - this.Width;
        }
        this.Top = SystemParameters.WorkArea.Top;
        this.Height = SystemParameters.WorkArea.Height;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.LoadSettings();
        ApplyWindowPosition(settings.StackPosition);
        _viewModel.ApplySettings(settings.StackPosition);
    }

    private async void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _isHovering = true;
        await Task.Delay(150);
        if (_isHovering && !_viewModel.IsExpanded)
        {
            _viewModel.IsExpanded = true;
            _viewModel.ResetScroll();
            
            // Wait for WPF to generate the new UI containers before animating
            await Application.Current.Dispatcher.InvokeAsync(() => {
                UpdateExpandedState(true);
            }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        }
    }

    private async void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        _isHovering = false;
        await Task.Delay(400); // Increased delay to prevent jitter over transparent gaps
        if (!_isHovering && _viewModel.IsExpanded)
        {
            _viewModel.IsExpanded = false;
            UpdateExpandedState(false);
        }
    }

    private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta < 0) _viewModel.Scroll(1);
        else _viewModel.Scroll(-1);
        
        UpdateExpandedState(_viewModel.IsExpanded);
    }

    private void UpdateExpandedState(bool expand)
    {
        var settings = _settingsService.LoadSettings();
        var ease = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        var duration = !settings.EnableAnimations
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(Math.Max(50, 450 - (settings.AnimationSpeed * 3)));
        
        for (int i = 0; i < NotesList.Items.Count; i++)
        {
            var container = NotesList.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
            if (container != null)
            {
                var border = FindVisualChild<Border>(container);
                if (border != null && border.DataContext is NoteItem item)
                {
                    System.Windows.Thickness targetMargin = expand ? item.ExpandedMargin : item.CollapsedMargin;
                    var anim = new System.Windows.Media.Animation.ThicknessAnimation(targetMargin, duration) { EasingFunction = ease };
                    border.BeginAnimation(FrameworkElement.MarginProperty, anim);
                }
            }
        }
        
        var addButton = (Border)this.FindName("AddButtonBorder");
        if (addButton != null)
        {
            System.Windows.Thickness targetAddMargin = expand ? _viewModel.ExpandedAddMargin : _viewModel.CollapsedAddMargin;
            var addAnim = new System.Windows.Media.Animation.ThicknessAnimation(targetAddMargin, duration) { EasingFunction = ease };
            AddButtonBorder.BeginAnimation(FrameworkElement.MarginProperty, addAnim);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}

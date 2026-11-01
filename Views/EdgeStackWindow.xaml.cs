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
    private readonly IFullScreenMonitorService _fullScreenMonitorService;
    private readonly MotionCoordinator _motionCoordinator = new();
    private bool _isHovering;

    public EdgeStackWindow(
        EdgeStackViewModel viewModel, 
        IEditorService editorService, 
        ISettingsService settingsService,
        IFullScreenMonitorService fullScreenMonitorService)
        : this(viewModel, editorService, settingsService, fullScreenMonitorService, true)
    {
    }

    internal EdgeStackWindow(
        EdgeStackViewModel viewModel, 
        IEditorService editorService, 
        ISettingsService settingsService,
        IFullScreenMonitorService fullScreenMonitorService,
        bool initializeComponent)
    {
        if (initializeComponent)
        {
            InitializeComponent();
        }
        _viewModel = viewModel;
        _editorService = editorService;
        _settingsService = settingsService;
        _fullScreenMonitorService = fullScreenMonitorService;
        DataContext = _viewModel;

        _settingsService.SettingsChanged += OnSettingsChanged;
        _fullScreenMonitorService.FullScreenChanged += OnFullScreenChanged;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnFullScreenChanged(object? sender, bool isFullScreen)
    {
        Dispatcher.InvokeAsync(() =>
        {
            FullScreenMonitorService.LogAudit($"[EVENT RECEIVED] FullScreenChanged: isFullScreen={isFullScreen}");
            UpdateVisibility();
        });
    }

    private void OnSettingsChanged()
    {
        Dispatcher.InvokeAsync(UpdateVisibility);
    }

    public void UpdateVisibility()
    {
        var settings = _settingsService.LoadSettings();
        FullScreenMonitorService.LogAudit($"[OVERLAY UPDATE] ShowNotesOnDesktop={settings.ShowNotesOnDesktop}, IsFullScreenActive={_fullScreenMonitorService.IsFullScreenActive}, Visibility={this.Visibility}");

        // Full-screen suppression has absolute priority over ordinary visibility conditions
        if (_fullScreenMonitorService.IsFullScreenActive)
        {
            _isHovering = false;
            if (_viewModel.IsExpanded)
            {
                _viewModel.IsExpanded = false;
            }
            this.Hide();
            FullScreenMonitorService.LogAudit("[OVERLAY ACTION] Executed Hide() due to active full-screen state.");
            return;
        }

        if (settings.ShowNotesOnDesktop)
        {
            ApplyWindowPosition(settings.StackPosition);
            _viewModel.ApplySettings(settings.StackPosition);
            UpdateExpandedState(_viewModel.IsExpanded);
            this.Topmost = true;
            this.Show();
            FullScreenMonitorService.LogAudit("[OVERLAY ACTION] Executed Show() (Restored to desktop).");
        }
        else
        {
            _isHovering = false;
            if (_viewModel.IsExpanded)
            {
                _viewModel.IsExpanded = false;
            }
            this.Hide();
            FullScreenMonitorService.LogAudit("[OVERLAY ACTION] Executed Hide() due to ShowNotesOnDesktop=False.");
        }
    }

    internal void ApplyWindowPosition(string stackPosition)
    {
        var workArea = SystemParameters.WorkArea;
        if (stackPosition == "Left")
        {
            this.Left = workArea.Left;
        }
        else
        {
            this.Left = workArea.Left + workArea.Width - this.Width;
        }
        this.Top = workArea.Top;
        this.Height = workArea.Height;
    }

    internal void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        void Action()
        {
            FullScreenMonitorService.LogAudit("[DISPLAY CHANGE] Screen resolution or monitor configuration changed. Repositioning EdgeStackWindow.");
            var settings = _settingsService.LoadSettings();
            ApplyWindowPosition(settings.StackPosition);
            UpdateVisibility();
        }

        if (Dispatcher.CheckAccess())
        {
            Action();
        }
        else
        {
            Dispatcher.InvokeAsync(Action);
        }
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        var settings = _settingsService.LoadSettings();
        ApplyWindowPosition(settings.StackPosition);
    }

    protected override void OnClosed(EventArgs e)
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _settingsService.SettingsChanged -= OnSettingsChanged;
        _fullScreenMonitorService.FullScreenChanged -= OnFullScreenChanged;
        base.OnClosed(e);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateVisibility();
    }

    private async void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_fullScreenMonitorService.IsFullScreenActive)
        {
            return;
        }

        _isHovering = true;
        await Task.Delay(150);
        if (_isHovering && !_viewModel.IsExpanded && !_fullScreenMonitorService.IsFullScreenActive)
        {
            _viewModel.IsExpanded = true;
            _viewModel.ResetScroll();
            
            // Wait for WPF to generate the new UI containers before animating
            await Application.Current.Dispatcher.InvokeAsync(() => {
                if (!_fullScreenMonitorService.IsFullScreenActive)
                {
                    UpdateExpandedState(true);
                }
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
        if (_fullScreenMonitorService.IsFullScreenActive)
        {
            return;
        }

        if (e.Delta < 0) _viewModel.Scroll(1);
        else _viewModel.Scroll(-1);
        
        UpdateExpandedState(_viewModel.IsExpanded);
    }

    private void UpdateExpandedState(bool expand)
    {
        if (NotesList == null) return;
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
                    double deltaX = _viewModel.IsLeft
                        ? (item.ExpandedMargin.Left - item.CollapsedMargin.Left)
                        : (item.CollapsedMargin.Right - item.ExpandedMargin.Right);

                    double targetX = expand ? deltaX : 0.0;

                    var transform = container.RenderTransform as TranslateTransform;
                    if (transform == null || transform.IsFrozen)
                    {
                        transform = new TranslateTransform();
                        container.RenderTransform = transform;
                    }

                    _motionCoordinator.AnimateX(container, transform, targetX, duration, ease);
                }
            }
        }
        
        var addButton = (Border)this.FindName("AddButtonBorder");
        if (addButton != null)
        {
            var transform = addButton.RenderTransform as TranslateTransform;
            if (transform == null)
            {
                if (addButton.RenderTransform is TransformGroup tg)
                {
                    transform = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                    if (transform == null)
                    {
                        transform = new TranslateTransform();
                        tg.Children.Add(transform);
                    }
                }
                else
                {
                    var tgNew = new TransformGroup();
                    if (addButton.RenderTransform != null && !addButton.RenderTransform.IsFrozen)
                    {
                        tgNew.Children.Add(addButton.RenderTransform);
                    }
                    transform = new TranslateTransform();
                    tgNew.Children.Add(transform);
                    addButton.RenderTransform = tgNew;
                }
            }

            double addDeltaX = _viewModel.IsLeft
                ? (_viewModel.ExpandedAddMargin.Left - _viewModel.CollapsedAddMargin.Left)
                : (_viewModel.CollapsedAddMargin.Right - _viewModel.ExpandedAddMargin.Right);

            double targetAddX = expand ? addDeltaX : 0.0;
            _motionCoordinator.AnimateX(addButton, transform, targetAddX, duration, ease);
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

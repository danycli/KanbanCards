using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using KanbanCards.Services;
using KanbanCards.ViewModels;

namespace KanbanCards.Views;

public interface INoteEditorWindow
{
    NoteEditorViewModel ViewModel { get; }
    double Left { get; set; }
    double Top { get; set; }
    double Width { get; }
    double Height { get; }
    void PlayPopAnimation();
    void AnimateGrabState(bool isGrabbed);
    bool Activate();
    void Show();
    void Close();
    event EventHandler Closed;
}

public partial class NoteEditorWindow : Window, INoteEditorWindow
{
    private bool _allowCloseAfterSave;
    private bool _closePending;
    private bool _isClosed;

    public NoteEditorViewModel ViewModel { get; }

    public NoteEditorWindow(NoteEditorViewModel viewModel) : this(viewModel, true)
    {
    }

    internal NoteEditorWindow(NoteEditorViewModel viewModel, bool initializeComponent)
    {
        if (initializeComponent)
        {
            InitializeComponent();
        }
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.RequestClose += Close;

        Loaded += (s, e) =>
        {
            if (TitleBox != null)
            {
                TitleBox.Focus();
                if (string.IsNullOrEmpty(ViewModel.Title))
                {
                    TitleBox.CaretIndex = 0;
                }
                else
                {
                    TitleBox.CaretIndex = TitleBox.Text.Length;
                }
            }
        };
    }

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_allowCloseAfterSave || ViewModel.IsNew)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_closePending) return;
        _closePending = true;
        bool saved;
        try { saved = await ViewModel.FlushAutosaveAsync(); }
        finally { _closePending = false; }
        if (!saved) return;

        _allowCloseAfterSave = true;
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_isClosed && IsVisible)
                Close();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        ViewModel.Dispose();
        base.OnClosed(e);
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        HandlePreviewKeyDown(e);
        base.OnPreviewKeyDown(e);
    }

    internal void HandlePreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            ViewModel.CloseCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Enter && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            ViewModel.OkCommand.Execute(null);
            e.Handled = true;
        }
    }

    internal void ResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        this.Width = Math.Max(this.MinWidth, this.Width + e.HorizontalChange);
        this.Height = Math.Max(this.MinHeight, this.Height + e.VerticalChange);
    }

    /// <summary>
    /// Animates the note into a tactile "lifted / grabbed" state while held and dragging,
    /// and smoothly settles it back onto the desktop when released.
    /// Uses hardware GPU transforms (Scale/Translate) to ensure 60fps drag with zero choppiness.
    /// </summary>
    public void AnimateGrabState(bool isGrabbed)
    {
        var duration = isGrabbed 
            ? TimeSpan.FromMilliseconds(70) 
            : TimeSpan.FromMilliseconds(120);
        IEasingFunction easing = isGrabbed 
            ? MotionSystem.QuadOut 
            : MotionSystem.CubicInOut;

        double targetScale = isGrabbed ? 1.05 : 1.0;
        double targetY = isGrabbed ? -5.0 : 0.0;

        var scaleAnim = new DoubleAnimation(targetScale, duration) { EasingFunction = easing };
        var translateYAnim = new DoubleAnimation(targetY, duration) { EasingFunction = easing };

        CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim, HandoffBehavior.SnapshotAndReplace);
        CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim, HandoffBehavior.SnapshotAndReplace);
        CardTranslate.BeginAnimation(TranslateTransform.YProperty, translateYAnim, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// Performs an instantaneous tactile pop-and-return feedback animation when clicked/reselected.
    /// </summary>
    public void PlayPopAnimation()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;

        var tExpand = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70));
        var tReturn = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180));

        var scaleAnim = new DoubleAnimationUsingKeyFrames();
        scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.05, tExpand, MotionSystem.QuadOut));
        scaleAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, tReturn, MotionSystem.CubicInOut));

        var translateYAnim = new DoubleAnimationUsingKeyFrames();
        translateYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(-5.0, tExpand, MotionSystem.QuadOut));
        translateYAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, tReturn, MotionSystem.CubicInOut));

        CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim, HandoffBehavior.SnapshotAndReplace);
        CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim, HandoffBehavior.SnapshotAndReplace);
        CardTranslate.BeginAnimation(TranslateTransform.YProperty, translateYAnim, HandoffBehavior.SnapshotAndReplace);
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            e.Handled = true;
            AnimateGrabState(isGrabbed: true);
            try
            {
                this.DragMove();
            }
            catch (InvalidOperationException)
            {
                // Ignored if mouse button was released before DragMove completed
            }
            finally
            {
                AnimateGrabState(isGrabbed: false);
            }
        }
    }
}

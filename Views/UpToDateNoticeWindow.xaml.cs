using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KanbanCards.Services;

namespace KanbanCards.Views;

public partial class UpToDateNoticeWindow : Window
{
    private const double ScreenMargin = 16.0;
    private bool _isClosing;
    private DispatcherTimer? _autoCloseTimer;

    public UpToDateNoticeWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => PositionBottomRight();
    }

    public UpToDateNoticeWindow(string version, string? message = null) : this()
    {
        VersionText.Text = version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? version : $"v{version}";
        if (!string.IsNullOrWhiteSpace(message))
        {
            MessageText.Text = message;
        }

        // Auto-close after 5 seconds if not interacted with
        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _autoCloseTimer.Tick += (_, _) =>
        {
            _autoCloseTimer.Stop();
            AnimateClose();
        };
        _autoCloseTimer.Start();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionBottomRight();
        AnimateEntrance();
    }

    private void PositionBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        var width = (ActualWidth > 0 && !double.IsNaN(ActualWidth)) ? ActualWidth : (double.IsNaN(Width) ? 360 : Width);
        var height = (ActualHeight > 0 && !double.IsNaN(ActualHeight)) ? ActualHeight : 180;

        Left = workArea.Right - width - ScreenMargin;
        Top = workArea.Bottom - height - ScreenMargin;
    }

    private void AnimateEntrance()
    {
        Opacity = 0;
        CardTranslate.Y = 25;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = MotionSystem.CubicOut
        };

        var slide = new DoubleAnimation(25, 0, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = MotionSystem.CubicOut
        };

        BeginAnimation(OpacityProperty, fade);
        CardTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer?.Stop();
        AnimateClose();
    }

    private void AnimateClose()
    {
        if (_isClosing) return;
        _isClosing = true;

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = MotionSystem.CubicIn
        };

        var slide = new DoubleAnimation(0, 20, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = MotionSystem.CubicIn
        };

        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
        CardTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
    }
}

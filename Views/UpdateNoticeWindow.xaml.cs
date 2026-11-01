using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KanbanCards.Services;
using KanbanCards.ViewModels;

namespace KanbanCards.Views;

public partial class UpdateNoticeWindow : Window
{
    private const double ScreenMargin = 16.0;
    private bool _isClosing;

    public UpdateNoticeWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => PositionBottomRight();
    }

    public UpdateNoticeWindow(UpdateNoticeViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += AnimateClose;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionBottomRight();
        AnimateEntrance();
    }

    private void PositionBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        var width = (ActualWidth > 0 && !double.IsNaN(ActualWidth)) ? ActualWidth : (double.IsNaN(Width) ? 380 : Width);
        var height = (ActualHeight > 0 && !double.IsNaN(ActualHeight)) ? ActualHeight : 260;

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

    public void AnimateClose()
    {
        if (_isClosing)
            return;

        _isClosing = true;

        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = MotionSystem.CubicIn
        };

        var slide = new DoubleAnimation(CardTranslate.Y, 20, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = MotionSystem.CubicIn
        };

        slide.Completed += (_, _) => Close();

        BeginAnimation(OpacityProperty, fade);
        CardTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
    }
}

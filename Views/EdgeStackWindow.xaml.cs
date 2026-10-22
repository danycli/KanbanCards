using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using KanbanCards.Models;
using KanbanCards.Services;
using KanbanCards.ViewModels;

namespace KanbanCards.Views;

public partial class EdgeStackWindow : Window
{
    private static readonly BrushConverter BrushConverter = new();

    private readonly EdgeStackViewModel _viewModel;
    private readonly IEditorService _editorService;
    private readonly ISettingsService _settingsService;
    private readonly MotionCoordinator _motionCoordinator = new();

    private readonly Stack<Border> _cardPool = new();
    private readonly Dictionary<int, Border> _activeCards = new();

    private CancellationTokenSource? _hoverCts;
    private int _wheelDeltaAccumulator = 0;

    public EdgeStackWindow(EdgeStackViewModel viewModel, IEditorService editorService, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _editorService = editorService;
        _settingsService = settingsService;
        DataContext = _viewModel;

        _settingsService.SettingsChanged += OnSettingsChanged;
        _viewModel.NotesUpdated += OnNotesUpdated;
        _viewModel.ScrollShifted += OnScrollShifted;
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
                ApplyViewportState(scrollDirection: 0, immediate: true);
                if (!IsVisible) this.Show();
            }
            else
            {
                this.Hide();
            }
        });
    }

    private void OnNotesUpdated()
    {
        Dispatcher.Invoke(() =>
        {
            ApplyViewportState(scrollDirection: 0, immediate: false);
        });
    }

    private void OnScrollShifted(int direction)
    {
        Dispatcher.Invoke(() =>
        {
            ApplyViewportState(scrollDirection: direction, immediate: false);
        });
    }

    private void ApplyWindowPosition(string stackPosition)
    {
        // Pinned to the primary monitor work area, compact and vertically centered
        var workArea = SystemParameters.WorkArea;
        this.Height = Math.Min(580, workArea.Height);
        this.Top = workArea.Top + (workArea.Height - this.Height) / 2.0;

        if (stackPosition == "Left")
        {
            this.Left = workArea.Left;
        }
        else
        {
            this.Left = workArea.Left + workArea.Width - this.Width;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.LoadSettings();
        ApplyWindowPosition(settings.StackPosition);
        _viewModel.ApplySettings(settings.StackPosition);

        var dpi = VisualTreeHelper.GetDpi(this);
        AddButtonBorder.CacheMode = new BitmapCache
        {
            EnableClearType = false,
            RenderAtScale = Math.Max(1.0, dpi.DpiScaleX),
            SnapsToDevicePixels = true
        };

        ApplyViewportState(scrollDirection: 0, immediate: true);
    }

    private async void StackArea_MouseEnter(object sender, MouseEventArgs e)
    {
        _hoverCts?.Cancel();
        _hoverCts = new CancellationTokenSource();
        var token = _hoverCts.Token;

        try
        {
            // Apple-tuned 160ms hover-entry delay to prevent accidental activation
            await Task.Delay(160, token);
            if (token.IsCancellationRequested) return;

            if (!_viewModel.IsExpanded)
            {
                _viewModel.IsExpanded = true;
                _viewModel.ResetScroll();
                ApplyViewportState(scrollDirection: 0, immediate: false);
            }
        }
        catch (TaskCanceledException)
        {
            // User moved away before delay expired
        }
    }

    private async void StackArea_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoverCts?.Cancel();
        _hoverCts = new CancellationTokenSource();
        var token = _hoverCts.Token;

        try
        {
            // Consistent 200ms exit delay: eliminates flicker when passing between cards
            await Task.Delay(200, token);
            if (token.IsCancellationRequested) return;

            if (_viewModel.IsExpanded)
            {
                _viewModel.IsExpanded = false;
                _viewModel.ResetScroll();
                ApplyViewportState(scrollDirection: 0, immediate: false);
            }
        }
        catch (TaskCanceledException)
        {
            // User returned before delay expired
        }
    }

    private void StackArea_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _wheelDeltaAccumulator += e.Delta;
        const int threshold = 120; // Standard Windows WHEEL_DELTA

        if (Math.Abs(_wheelDeltaAccumulator) >= threshold)
        {
            int steps = _wheelDeltaAccumulator / threshold;
            _wheelDeltaAccumulator %= threshold;

            // Negative delta means scrolling down (toward older notes) -> step > 0
            int direction = -steps;
            _viewModel.Scroll(direction);
        }
        e.Handled = true;
    }

    private void ApplyViewportState(int scrollDirection, bool immediate)
    {
        var settings = _settingsService.LoadSettings();
        bool isExpanded = _viewModel.IsExpanded;
        bool isLeft = _viewModel.IsLeft;
        var duration = immediate
            ? TimeSpan.Zero
            : (scrollDirection != 0
                ? MotionSystem.ScrollShiftDuration(settings)
                : (isExpanded ? MotionSystem.HoverExpandDuration(settings) : MotionSystem.HoverCollapseDuration(settings)));

        var visibleNotes = _viewModel.VisibleNotes;
        var currentIds = new HashSet<int>();

        // 1. Position / Animate visible cards to their target slots
        for (int slot = 0; slot < visibleNotes.Count; slot++)
        {
            var item = visibleNotes[slot];
            var note = item.Note;
            currentIds.Add(note.Id);

            double targetY = slot * 50.0;
            double targetX = MotionSystem.GetCardTargetX(slot, isExpanded, isLeft);
            int targetZ = 100 - slot;

            if (_activeCards.TryGetValue(note.Id, out var existingCard))
            {
                BindCard(existingCard, note);
                Panel.SetZIndex(existingCard, targetZ);

                var translate = GetTranslateTransform(existingCard);
                if (translate != null)
                {
                    _motionCoordinator.AnimateX(existingCard, translate, targetX, duration, MotionSystem.CubicOut);
                    _motionCoordinator.AnimateY(existingCard, translate, targetY, duration, MotionSystem.CubicOut);
                    _motionCoordinator.AnimateOpacity(existingCard, 1.0, duration, MotionSystem.CubicOut);
                }
            }
            else
            {
                var newCard = GetOrCreateCard(note);
                _activeCards[note.Id] = newCard;
                Panel.SetZIndex(newCard, targetZ);

                var translate = GetTranslateTransform(newCard);
                if (translate != null)
                {
                    if (immediate || scrollDirection == 0)
                    {
                        _motionCoordinator.CancelAndSet(newCard, translate, targetX, targetY, 1.0);
                    }
                    else
                    {
                        double entryY = scrollDirection > 0 ? (visibleNotes.Count * 50.0) : -50.0;
                        _motionCoordinator.CancelAndSet(newCard, translate, targetX, entryY, 0.0);
                        _motionCoordinator.AnimateY(newCard, translate, targetY, duration, MotionSystem.CubicOut);
                        _motionCoordinator.AnimateOpacity(newCard, 1.0, duration, MotionSystem.CubicOut);
                    }
                }

                if (!NotesCanvas.Children.Contains(newCard))
                {
                    NotesCanvas.Children.Add(newCard);
                }
            }
        }

        // 2. Animate and recycle cards that exited the viewport
        var exitedIds = _activeCards.Keys.Where(id => !currentIds.Contains(id)).ToList();
        foreach (var id in exitedIds)
        {
            var card = _activeCards[id];
            _activeCards.Remove(id);

            if (immediate || duration <= TimeSpan.Zero)
            {
                RecycleCard(card);
            }
            else
            {
                var translate = GetTranslateTransform(card);
                if (translate != null)
                {
                    double exitY = scrollDirection > 0 ? -50.0 : (visibleNotes.Count * 50.0);
                    _motionCoordinator.AnimateY(card, translate, exitY, duration, MotionSystem.CubicOut);
                    _motionCoordinator.AnimateOpacity(card, 0.0, duration, MotionSystem.CubicOut, completed: () =>
                    {
                        RecycleCard(card);
                    });
                }
                else
                {
                    RecycleCard(card);
                }
            }
        }

        // 3. Update Add Button position and depth
        int activeCount = Math.Min(visibleNotes.Count, 10);
        double addTargetY = activeCount * 50.0;
        double addTargetX = MotionSystem.GetAddButtonTargetX(activeCount, isExpanded, isLeft);

        var addTranslate = GetTranslateTransform(AddButtonBorder);
        if (addTranslate != null)
        {
            _motionCoordinator.AnimateX(AddButtonBorder, addTranslate, addTargetX, duration, MotionSystem.CubicOut);
            _motionCoordinator.AnimateY(AddButtonBorder, addTranslate, addTargetY, duration, MotionSystem.CubicOut);
        }

        // 4. Update canvas heights to cleanly fit content
        NotesCanvas.Height = Math.Max(0, activeCount * 50.0);
        StackCanvas.Height = (activeCount + 1) * 50.0;
    }

    private Border GetOrCreateCard(Note note)
    {
        Border card;
        if (_cardPool.Count > 0)
        {
            card = _cardPool.Pop();
        }
        else
        {
            card = CreateCardElement();
        }
        BindCard(card, note);
        return card;
    }

    private void RecycleCard(Border card)
    {
        NotesCanvas.Children.Remove(card);
        _motionCoordinator.Cleanup(card);
        _cardPool.Push(card);
    }

    private Border CreateCardElement()
    {
        var border = new Border
        {
            Width = 280,
            Height = 48,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)),
            Cursor = Cursors.Hand
        };
        border.SetBinding(Border.CornerRadiusProperty, new Binding(nameof(_viewModel.NoteCornerRadius)) { Source = _viewModel });
        border.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(_viewModel.NoteBorderThickness)) { Source = _viewModel });
        border.SetBinding(UIElement.RenderTransformOriginProperty, new Binding(nameof(_viewModel.RenderTransformOrigin)) { Source = _viewModel });

        var scale = new ScaleTransform(1, 1);
        var translate = new TranslateTransform(0, 0);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(translate);
        border.RenderTransform = group;

        border.Effect = new DropShadowEffect
        {
            Color = Colors.Black,
            Opacity = 0.15,
            ShadowDepth = 2,
            BlurRadius = 6,
            Direction = 270,
            RenderingBias = RenderingBias.Performance
        };

        var dpi = VisualTreeHelper.GetDpi(this);
        border.CacheMode = new BitmapCache
        {
            EnableClearType = false,
            RenderAtScale = Math.Max(1.0, dpi.DpiScaleX),
            SnapsToDevicePixels = true
        };

        var grid = new Grid { ClipToBounds = true, IsHitTestVisible = false };
        var textBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 240,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27)),
            FontWeight = FontWeights.SemiBold,
            FontSize = 14
        };
        textBlock.SetBinding(TextBlock.MarginProperty, new Binding(nameof(_viewModel.TitleMargin)) { Source = _viewModel });
        grid.Children.Add(textBlock);
        border.Child = grid;

        border.MouseEnter += Card_MouseEnter;
        border.MouseLeave += Card_MouseLeave;
        border.MouseLeftButtonUp += Card_MouseLeftButtonUp;

        return border;
    }

    private void BindCard(Border border, Note note)
    {
        border.Tag = note;
        try
        {
            border.Background = (Brush)BrushConverter.ConvertFromString(note.Color)!;
        }
        catch
        {
            border.Background = new SolidColorBrush(Color.FromRgb(0x90, 0xCA, 0xF9));
        }

        if (border.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is TextBlock textBlock)
        {
            textBlock.Text = note.DisplayTitle;
        }
    }

    private int GetSlotIndexForNote(int noteId)
    {
        for (int i = 0; i < _viewModel.VisibleNotes.Count; i++)
        {
            if (_viewModel.VisibleNotes[i].Note.Id == noteId) return i;
        }
        return -1;
    }

    private void Card_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            Panel.SetZIndex(border, 250);
            var scale = GetScaleTransform(border);
            if (scale != null)
            {
                var settings = _settingsService.LoadSettings();
                var duration = MotionSystem.HoverScaleDuration(settings);
                _motionCoordinator.AnimateScale(border, scale, 1.025, duration, MotionSystem.QuadOut);
            }
        }
    }

    private void Card_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border border && border.Tag is Note note)
        {
            int slot = GetSlotIndexForNote(note.Id);
            Panel.SetZIndex(border, slot >= 0 ? 100 - slot : 0);

            var scale = GetScaleTransform(border);
            if (scale != null)
            {
                var settings = _settingsService.LoadSettings();
                var duration = MotionSystem.HoverScaleDuration(settings);
                _motionCoordinator.AnimateScale(border, scale, 1.0, duration, MotionSystem.QuadIn);
            }
        }
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is Note note)
        {
            _viewModel.EditNoteCommand.Execute(note);
            e.Handled = true;
        }
    }

    private void AddButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            var settings = _settingsService.LoadSettings();
            var duration = MotionSystem.HoverScaleDuration(settings);
            var scale = GetScaleTransform(border);
            if (scale != null)
            {
                _motionCoordinator.AnimateScale(border, scale, 1.03, duration, MotionSystem.QuadOut);
            }
        }
    }

    private void AddButton_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border border)
        {
            var settings = _settingsService.LoadSettings();
            var duration = MotionSystem.HoverScaleDuration(settings);
            var scale = GetScaleTransform(border);
            if (scale != null)
            {
                _motionCoordinator.AnimateScale(border, scale, 1.0, duration, MotionSystem.QuadIn);
            }
        }
    }

    private void AddButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _viewModel.AddNoteCommand.Execute(null);
        e.Handled = true;
    }

    private static TranslateTransform? GetTranslateTransform(UIElement element)
    {
        if (element.RenderTransform is TransformGroup group)
        {
            foreach (var child in group.Children)
            {
                if (child is TranslateTransform tt) return tt;
            }
        }
        return element.RenderTransform as TranslateTransform;
    }

    private static ScaleTransform? GetScaleTransform(UIElement element)
    {
        if (element.RenderTransform is TransformGroup group)
        {
            foreach (var child in group.Children)
            {
                if (child is ScaleTransform st) return st;
            }
        }
        return element.RenderTransform as ScaleTransform;
    }
}

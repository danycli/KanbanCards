using KanbanCards.Models;
using KanbanCards.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace KanbanCards.Services;

public interface IReminderService
{
    void Start();
    void Stop();
}

/// <summary>Checks due cards on startup and once per minute; missed reminders are shown once, never in a burst.</summary>
public sealed class ReminderService : IReminderService
{
    private readonly INoteService _notes;
    private readonly ISettingsService _settings;
    private readonly IFullScreenMonitorService? _fullScreenMonitor;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };
    private bool _checking;

    public ReminderService(INoteService notes, ISettingsService settings, IFullScreenMonitorService? fullScreenMonitor = null)
    {
        _notes = notes;
        _settings = settings;
        _fullScreenMonitor = fullScreenMonitor;
        _timer.Tick += async (_, _) => await CheckAsync();
    }

    public void Start() { _timer.Start(); _ = CheckAsync(); }
    public void Stop() => _timer.Stop();

    private async Task CheckAsync()
    {
        if (_checking || !_settings.LoadSettings().EnableReminders || _fullScreenMonitor?.IsFullScreenActive == true) return;
        _checking = true;
        try
        {
            var now = DateTime.UtcNow;
            var due = (await _notes.GetAllNotesAsync()).Where(note => ShouldShowReminder(note, now))
                .OrderBy(note => note.DueAt).Take(1).ToList();
            foreach (var note in due)
            {
                note.ReminderSentAt = now;
                note.ReminderSnoozedUntil = null;
                await _notes.UpdateNoteAsync(note);
                ShowToast(note);
            }
        }
        catch (Exception ex) { FullScreenMonitorService.LogAudit($"[REMINDER] Check failed: {ex.Message}"); }
        finally { _checking = false; }
    }

    private void ShowToast(Note note)
    {
        var window = new Window
        {
            Width = 350, Height = 174, WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = Brushes.Transparent, Topmost = true, ShowActivated = false,
            ShowInTaskbar = false, ResizeMode = ResizeMode.NoResize, Title = "Kanban Cards reminder"
        };
        window.Left = SystemParameters.WorkArea.Right - window.Width - 18;
        window.Top = SystemParameters.WorkArea.Bottom - window.Height - 18;
        var outer = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(16) };
        outer.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 18, ShadowDepth = 3, Opacity = .14 };
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var label = new TextBlock { Text = "DUE REMINDER", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(99, 102, 241)) };
        var title = new TextBlock { Text = note.Title, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 7, 0, 8) };
        Grid.SetRow(title, 1);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var snooze = new Button { Content = "Snooze 10 min", Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush(Color.FromRgb(238, 242, 255)), BorderThickness = new Thickness(0), Style = Application.Current.TryFindResource("SecondaryButtonStyle") as Style };
        var dismiss = new Button { Content = "Dismiss", Padding = new Thickness(10, 6, 10, 6), Background = new SolidColorBrush(Color.FromRgb(99, 102, 241)), Foreground = Brushes.White, BorderThickness = new Thickness(0), Style = Application.Current.TryFindResource("PrimaryButtonStyle") as Style };
        snooze.Click += async (_, _) =>
        {
            Snooze(note, DateTime.UtcNow);
            await _notes.UpdateNoteAsync(note);
            window.Close();
        };
        dismiss.Click += (_, _) => window.Close();
        actions.Children.Add(snooze); actions.Children.Add(dismiss); Grid.SetRow(actions, 2);
        layout.Children.Add(label); layout.Children.Add(title); layout.Children.Add(actions); outer.Child = layout; window.Content = outer;
        window.Show();
        var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        closeTimer.Tick += (_, _) => { closeTimer.Stop(); window.Close(); };
        closeTimer.Start();
    }

    internal static bool ShouldShowReminder(Note note, DateTime utcNow) =>
        note.ReminderEnabled && !note.IsArchived && note.DueAt.HasValue && note.DueAt.Value <= utcNow &&
        (note.ReminderSentAt == null || note.ReminderSnoozedUntil.HasValue && note.ReminderSnoozedUntil.Value <= utcNow);

    internal static void Snooze(Note note, DateTime utcNow)
    {
        note.ReminderSnoozedUntil = utcNow.AddMinutes(10);
    }
}

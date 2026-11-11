using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KanbanCards.Models;
using KanbanCards.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace KanbanCards;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private Point _dragStartPoint;
    private Models.Note? _dragNote;
    private ListBox? _dragSourceLane;
    private bool _dragInProgress;

    private void CloseDataMenu()
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.IsDataMenuOpen = false;
    }

    private void TemplateMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string template } || DataContext is not MainViewModel viewModel)
            return;

        viewModel.CreateTemplateCommand.Execute(template);
    }

    private void TemplatePicker_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.Dispatcher.BeginInvoke(new Action(() => menu.IsOpen = true), System.Windows.Threading.DispatcherPriority.Background);
        }
    }


    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        CloseDataMenu();
        var dialog = new SaveFileDialog { Title = "Export Kanban Cards", Filter = "JSON export (*.json)|*.json", FileName = "kanban-cards-export.json", DefaultExt = ".json", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        try { await ((MainViewModel)DataContext).ExportJsonAsync(dialog.FileName); Services.AppToastService.Show("JSON export complete", Path.GetFileName(dialog.FileName)); }
        catch (Exception ex) { System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Kanban Cards", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        CloseDataMenu();
        var dialog = new SaveFileDialog { Title = "Export Kanban Cards", Filter = "Markdown file (*.md)|*.md", FileName = "kanban-cards-export.md", DefaultExt = ".md", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        try { await ((MainViewModel)DataContext).ExportMarkdownAsync(dialog.FileName); Services.AppToastService.Show("Markdown export complete", Path.GetFileName(dialog.FileName)); }
        catch (Exception ex) { System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Kanban Cards", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void ImportJson_Click(object sender, RoutedEventArgs e)
    {
        CloseDataMenu();
        var dialog = new OpenFileDialog { Title = "Import Kanban Cards", Filter = "Kanban Cards JSON (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var vm = (MainViewModel)DataContext;
            var preview = await vm.PreviewImportAsync(dialog.FileName);
            if (preview.NewNotes == 0)
            {
                Services.AppToastService.Show("Nothing new to import", $"{preview.Duplicates} matching note(s) were already on this board.", false);
                return;
            }
            var sample = string.IsNullOrWhiteSpace(preview.SampleTitles) ? string.Empty : $"\n\n{preview.SampleTitles}";
            var prompt = $"Import {preview.NewNotes} new note(s)? {preview.Duplicates} duplicate(s) will be skipped.{sample}\n\nYour existing cards will stay in place.";
            if (System.Windows.MessageBox.Show(prompt, "Import preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            var count = await vm.ImportJsonAsync(dialog.FileName);
            Services.AppToastService.Show("Import complete", $"Added {count} note{(count == 1 ? string.Empty : "s")}; existing matches were skipped.");
        }
        catch (Exception ex) { System.Windows.MessageBox.Show($"Import failed. Your current notes were left unchanged.\n\n{ex.Message}", "Kanban Cards", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        CloseDataMenu();
        if (System.Windows.MessageBox.Show("Restore the most recent automatic backup? Kanban Cards will update its database to the current version. A safety copy of your current data is kept first.", "Restore backup", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            var dbPath = Path.Combine(Services.ApplicationDataPaths.DataDirectory, "kanbancards.db");
            Services.DatabaseBackupService.RestoreLatestBackup(dbPath, () =>
            {
                var factory = _serviceProvider.GetRequiredService<IDbContextFactory<Data.KanbanCardsDbContext>>();
                using var context = factory.CreateDbContext();
                context.Database.Migrate();
            });
            await ((MainViewModel)DataContext).LoadNotesAsync();
            Services.AppToastService.Show("Backup restored", "Your board is back to the saved version.");
        }
        catch (Exception ex) { System.Windows.MessageBox.Show($"Restore failed: {ex.Message}", "Kanban Cards", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void OpenBackupFolder_Click(object sender, RoutedEventArgs e)
    {
        CloseDataMenu();
        try
        {
            var path = Path.Combine(Services.ApplicationDataPaths.DataDirectory, "backups");
            Directory.CreateDirectory(path);
            var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            startInfo.ArgumentList.Add(path);
            Process.Start(startInfo);
        }
        catch (Exception ex) { System.Windows.MessageBox.Show($"Could not open the backup folder: {ex.Message}", "Kanban Cards", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
        : this(viewModel, serviceProvider, true)
    {
    }

    internal MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider, bool initializeComponent)
    {
        if (initializeComponent)
        {
            InitializeComponent();
        }
        _serviceProvider = serviceProvider;
        DataContext = viewModel;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            this.DragMove();
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var existing = System.Windows.Application.Current?.Windows.OfType<Views.SettingsWindow>().FirstOrDefault();
        if (existing != null)
        {
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }
            existing.Activate();
            return;
        }

        ModalDimmer.Visibility = Visibility.Visible;
        UpdateLayout();
        try
        {
            var settingsWindow = _serviceProvider.GetRequiredService<Views.SettingsWindow>();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }
        finally
        {
            ModalDimmer.Visibility = Visibility.Collapsed;
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void NoteCard_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_dragInProgress ||
            e.OriginalSource is DependencyObject source &&
            (FindVisualAncestor<ComboBox>(source) != null || FindVisualAncestor<Button>(source) != null))
            return;

        if (sender is FrameworkElement { DataContext: Models.Note note } && DataContext is MainViewModel viewModel)
        {
            viewModel.EditNoteCommand.Execute(note);
            e.Handled = true;
        }
    }

    private void StatusCombo_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.DataContext is not Models.Note note)
            return;

        combo.Tag = true;
        combo.SelectedItem = note.Status;
        combo.Tag = false;
    }

    private async void StatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.Tag is true ||
            combo.DataContext is not Models.Note note ||
            combo.SelectedItem is not NoteStatus status ||
            DataContext is not MainViewModel viewModel || note.Status == status)
            return;

        try
        {
            await viewModel.MoveNoteToStatusAsync(note, status);
        }
        catch (Exception ex)
        {
            Services.FullScreenMonitorService.LogAudit($"[BOARD STATUS] Failed to move note {note.Id}: {ex.Message}");
            await viewModel.LoadNotesAsync();
        }
    }

    private void Lane_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox selectedLane || selectedLane.SelectedItem == null)
            return;

        foreach (var lane in GetLaneLists())
        {
            if (!ReferenceEquals(lane, selectedLane))
                lane.SelectedItem = null;
        }
    }

    private void Lane_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not ListBox lane || DataContext is not MainViewModel viewModel || !viewModel.CanReorder ||
            e.OriginalSource is not DependencyObject source ||
            FindVisualAncestor<ComboBox>(source) != null || FindVisualAncestor<Button>(source) != null)
        {
            _dragNote = null;
            _dragSourceLane = null;
            return;
        }

        if (ItemsControl.ContainerFromElement(lane, source) is ListBoxItem container && container.DataContext is Models.Note note)
        {
            _dragNote = note;
            _dragSourceLane = lane;
            _dragStartPoint = e.GetPosition(lane);
        }
    }

    private void Lane_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not ListBox lane || !ReferenceEquals(lane, _dragSourceLane) || _dragNote == null ||
            e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
            return;

        var current = e.GetPosition(lane);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _dragInProgress = true;
        try
        {
            var data = new DataObject(typeof(Models.Note), _dragNote);
            DragDrop.DoDragDrop(lane, data, DragDropEffects.Move);
        }
        finally
        {
            _dragInProgress = false;
            _dragNote = null;
            _dragSourceLane = null;
        }
    }

    private void Lane_DragOver(object sender, DragEventArgs e)
    {
        var draggedNote = e.Data.GetData(typeof(Models.Note)) as Models.Note;
        var laneStatus = (sender as ListBox)?.Tag;
        if (draggedNote != null && laneStatus is NoteStatus status && Enum.IsDefined(status))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void Lane_Drop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox lane || lane.Tag is not NoteStatus status ||
            !Enum.IsDefined(status) || e.Data.GetData(typeof(Models.Note)) is not Models.Note draggedNote ||
            DataContext is not MainViewModel viewModel || !viewModel.CanReorder)
            return;

        var targetContainer = e.OriginalSource is DependencyObject source
            ? ItemsControl.ContainerFromElement(lane, source) as ListBoxItem
            : null;
        var targetNote = targetContainer?.DataContext as Models.Note;
        if (targetNote?.Id == draggedNote.Id)
        {
            e.Handled = true;
            return;
        }
        e.Handled = true;

        try
        {
            await viewModel.MoveNoteToLaneAsync(draggedNote, status, targetNote?.Id);
        }
        catch (Exception ex)
        {
            Services.FullScreenMonitorService.LogAudit($"[BOARD DROP] Failed to move note {draggedNote.Id} to {status}: {ex.Message}");
            await viewModel.LoadNotesAsync();
        }
    }

    private ListBox[] GetLaneLists() => new[] { InboxListBox, NextListBox, DoingListBox, DoneListBox }
        .Where(list => list != null)
        .Cast<ListBox>()
        .ToArray();

    private Models.Note? GetSelectedNote() => GetLaneLists()
        .Select(list => list.SelectedItem)
        .OfType<Models.Note>()
        .FirstOrDefault();

    private static T? FindVisualAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
                return match;
            current = current is Visual || current is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    internal void HandleClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!App.IsShuttingDown)
        {
            e.Cancel = true;
            this.Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        var vm = DataContext as MainViewModel;

        // Escape: If SearchInput has text, clear search; otherwise hide/close window
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            if (vm != null && !string.IsNullOrEmpty(vm.SearchText))
            {
                vm.ClearSearchCommand.Execute(null);
                e.Handled = true;
                return;
            }
            Close();
            e.Handled = true;
            return;
        }

        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control &&
            e.Key is System.Windows.Input.Key.Up or System.Windows.Input.Key.Down && GetSelectedNote() is Models.Note orderNote)
        {
            if (vm?.CanReorder == true)
            {
                _ = vm.MoveNoteInLaneAsync(orderNote, e.Key == System.Windows.Input.Key.Up ? -1 : 1);
                e.Handled = true;
                return;
            }
        }

        // Ctrl+F: Focus search box
        if (e.Key == System.Windows.Input.Key.F && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            if (SearchInput != null)
            {
                SearchInput.Focus();
                SearchInput.SelectAll();
                e.Handled = true;
                return;
            }
        }

        // Ctrl+N: Add note
        if (e.Key == System.Windows.Input.Key.N && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            vm?.AddNoteCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Enter: If a note is selected in ListBox and SearchInput does not have focus
        if (e.Key == System.Windows.Input.Key.Enter && (SearchInput == null || !SearchInput.IsFocused))
        {
            if (GetSelectedNote() is Models.Note selectedNote)
            {
                vm?.EditNoteCommand.Execute(selectedNote);
                e.Handled = true;
                return;
            }
        }

        // Delete: If a note is selected in ListBox and SearchInput does not have focus
        if (e.Key == System.Windows.Input.Key.Delete && (SearchInput == null || !SearchInput.IsFocused))
        {
            if (GetSelectedNote() is Models.Note selectedNote)
            {
                if (Views.ConfirmDeleteDialog.ShowConfirmation(this))
                    vm?.DeleteNoteCommand.Execute(selectedNote);
                e.Handled = true;
                return;
            }
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        HandleClosing(e);
    }
}

using System;
using System.Linq;
using System.Windows;
using KanbanCards.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KanbanCards;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;

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
            if (NotesListBox?.SelectedItem is Models.Note selectedNote)
            {
                vm?.EditNoteCommand.Execute(selectedNote);
                e.Handled = true;
                return;
            }
        }

        // Delete: If a note is selected in ListBox and SearchInput does not have focus
        if (e.Key == System.Windows.Input.Key.Delete && (SearchInput == null || !SearchInput.IsFocused))
        {
            if (NotesListBox?.SelectedItem is Models.Note selectedNote)
            {
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
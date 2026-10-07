using System.Windows;
using KanbanCards.ViewModels;

namespace KanbanCards.Views;

public partial class NoteEditorWindow : Window
{
    public NoteEditorWindow(NoteEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            this.DragMove();
        }
    }
}

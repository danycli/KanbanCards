using System.Windows;
using System.Windows.Input;

namespace KanbanCards.Views;

public partial class ConfirmDeleteDialog : Window
{
    public ConfirmDeleteDialog()
    {
        InitializeComponent();
    }

    public static bool ShowConfirmation(Window? owner = null)
    {
        var dialog = new ConfirmDeleteDialog();
        if (owner != null && owner.IsVisible)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog() == true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        DragMove();
    }
}

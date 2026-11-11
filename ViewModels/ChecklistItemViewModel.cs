using System;

namespace KanbanCards.ViewModels;

public sealed class ChecklistItemViewModel : ViewModelBase
{
    private string _text = string.Empty;
    private bool _isDone;
    public string Text { get => _text; set => SetProperty(ref _text, value); }
    public bool IsDone { get => _isDone; set { if (SetProperty(ref _isDone, value)) OnPropertyChanged(nameof(StateText)); } }
    public string StateText => IsDone ? "Complete" : "Not complete";
}

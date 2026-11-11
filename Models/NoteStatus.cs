namespace KanbanCards.Models;

/// <summary>
/// The lightweight workflow state for a note on the Kanban board.
/// Values are persisted as integers; keep existing numeric values stable.
/// </summary>
public enum NoteStatus
{
    Inbox = 0,
    Next = 1,
    Doing = 2,
    Done = 3
}

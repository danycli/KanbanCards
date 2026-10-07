using System;

namespace KanbanCards.Models;

public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#90CAF9";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DisplayTitle 
    {
        get 
        {
            if (string.IsNullOrWhiteSpace(Title)) return "";
            if (Title.Length <= 20) return Title;
            return Title.Substring(0, 17) + "...";
        }
    }
}

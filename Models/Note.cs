using System;
using System.Linq;

namespace KanbanCards.Models;

public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#90CAF9";
    public NoteStatus Status { get; set; } = NoteStatus.Inbox;
    public int SortOrder { get; set; }
    public bool IsPinned { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public int Priority { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? ReminderSentAt { get; set; }
    public DateTime? ReminderSnoozedUntil { get; set; }
    public bool ReminderEnabled { get; set; }
    public string TagsJson { get; set; } = "[]";
    public string ChecklistJson { get; set; } = "[]";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsArchived => ArchivedAt.HasValue;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string PriorityLabel => Priority switch { 2 => "Urgent", 1 => "Important", _ => "Normal" };
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DueLabel => DueAt is null ? string.Empty : DueAt.Value.ToLocalTime().ToString("MMM d · h:mm tt");
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string ChecklistSummary
    {
        get
        {
            try
            {
                var items = System.Text.Json.JsonSerializer.Deserialize<string[]>(ChecklistJson) ?? Array.Empty<string>();
                if (items.Length == 0) return string.Empty;
                var done = items.Count(item => item.StartsWith("[x]", StringComparison.OrdinalIgnoreCase));
                return $"{done}/{items.Length} steps";
            }
            catch { return string.Empty; }
        }
    }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string TagsLabel
    {
        get { try { return string.Join(" · ", System.Text.Json.JsonSerializer.Deserialize<string[]>(TagsJson) ?? Array.Empty<string>()); } catch { return string.Empty; } }
    }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool HasTags => !string.IsNullOrWhiteSpace(TagsLabel);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DisplayTitle 
    {
        get 
        {
            if (string.IsNullOrWhiteSpace(Title)) return "";
            var clean = System.Text.RegularExpressions.Regex.Replace(Title, @"\s+", " ").Trim();
            if (clean.Length <= 20) return clean;
            return clean.Substring(0, 17) + "...";
        }
    }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string FormattedDate
    {
        get
        {
            var local = UpdatedAt.ToLocalTime();
            var now = DateTime.Now;
            if (local.Date == now.Date)
            {
                return local.ToString("h:mm tt");
            }
            if (local.Year == now.Year)
            {
                return local.ToString("MMM d");
            }
            return local.ToString("MMM d, yyyy");
        }
    }
}

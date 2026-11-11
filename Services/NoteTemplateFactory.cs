using System;
using System.Text.Json;
using KanbanCards.Models;

namespace KanbanCards.Services;

public static class NoteTemplateFactory
{
    public static Note Create(string templateId) => templateId.ToLowerInvariant() switch
    {
        "meeting" => new Note
        {
            Title = "Meeting notes",
            Description = "Attendees:\n\nTopics:\n\nDecisions:\n\nNext steps:",
            ChecklistJson = Checklist("Capture decisions", "Assign next steps")
        },
        "daily" => new Note
        {
            Title = "Daily plan",
            Description = "Today:\n\nNotes:",
            ChecklistJson = Checklist("One important thing", "A small follow-up")
        },
        _ => new Note
        {
            Title = "Idea",
            Description = "What is the idea?\n\nWhy it matters:\n\nFirst small step:",
            ChecklistJson = Checklist("Define a first small step")
        }
    };

    private static string Checklist(params string[] steps) =>
        JsonSerializer.Serialize(Array.ConvertAll(steps, step => $"[ ] {step}"));
}

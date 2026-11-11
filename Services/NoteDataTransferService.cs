using KanbanCards.Data;
using KanbanCards.Models;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KanbanCards.Services;

public sealed class NoteDataTransferService
{
    private const int FormatVersion = 1;
    private const long MaximumImportBytes = 10 * 1024 * 1024;
    private readonly INoteService _notes;
    public NoteDataTransferService(INoteService notes) => _notes = notes;

    public async Task ExportJsonAsync(string path)
    {
        var document = new NoteExportDocument { Version = FormatVersion, ExportedAt = DateTime.UtcNow, Notes = await _notes.GetAllNotesAsync() };
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        await AtomicWriteAsync(path, json);
    }

    public async Task ExportMarkdownAsync(string path)
    {
        var notes = await _notes.GetAllNotesAsync();
        var markdown = new StringBuilder("# Kanban Cards export\n\n")
            .Append("Exported: ").AppendLine(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm zzz"))
            .Append("Cards: ").AppendLine(notes.Count.ToString())
            .AppendLine();

        if (notes.Count == 0)
            markdown.AppendLine("_No cards to export._").AppendLine();

        foreach (var note in notes)
        {
            markdown.Append("## ").AppendLine(string.IsNullOrWhiteSpace(note.Title)
                ? "Untitled card"
                : note.Title.Replace("\r", " ").Replace("\n", " "));
            markdown.Append("- Lane: ").AppendLine(note.Status.ToString());
            markdown.Append("- Priority: ").AppendLine(note.PriorityLabel);
            markdown.Append("- Color: `").Append(note.Color).AppendLine("`");
            markdown.Append("- Pinned: ").AppendLine(note.IsPinned ? "Yes" : "No");
            if (note.DueAt is not null) markdown.Append("- Due: ").AppendLine(note.DueAt.Value.ToLocalTime().ToString("yyyy-MM-dd h:mm tt"));
            if (note.ReminderEnabled) markdown.AppendLine("- Reminder: Enabled");
            if (note.IsArchived) markdown.AppendLine("- Archived: Yes");
            markdown.Append("- Created: ").AppendLine(note.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd h:mm tt"));
            markdown.Append("- Updated: ").AppendLine(note.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd h:mm tt"));

            if (!string.IsNullOrWhiteSpace(note.Description))
                markdown.AppendLine().AppendLine(note.Description.TrimEnd()).AppendLine();

            var tags = ReadStringArray(note.TagsJson);
            if (tags.Length > 0)
                markdown.Append("Tags: ").AppendLine(string.Join(", ", tags));

            var checklist = ReadStringArray(note.ChecklistJson);
            foreach (var item in checklist)
            {
                var done = item.StartsWith("[x]", StringComparison.OrdinalIgnoreCase);
                var text = item.StartsWith("[x]", StringComparison.OrdinalIgnoreCase) || item.StartsWith("[ ]", StringComparison.Ordinal)
                    ? item[3..].TrimStart()
                    : item;
                markdown.Append("- [").Append(done ? 'x' : ' ').Append("] ").AppendLine(text);
            }

            markdown.AppendLine("---").AppendLine();
        }
        await AtomicWriteAsync(path, markdown.ToString());
    }

    public async Task<int> ImportJsonAsync(string path)
    {
        var document = await ReadDocumentAsync(path);

        var existing = await _notes.GetAllNotesAsync();
        var keys = existing.Select(note => Key(note.Title, note.Description)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var imports = new System.Collections.Generic.List<Note>();
        foreach (var source in document.Notes)
        {
            if (source is null)
                throw new InvalidDataException("This export contains an invalid note entry. No notes were imported.");
            if (string.IsNullOrWhiteSpace(source.Title) || !keys.Add(Key(source.Title, source.Description))) continue;
            imports.Add(new Note
            {
                Title = source.Title.Trim(), Description = source.Description ?? string.Empty,
                Color = IsHexColor(source.Color) ? source.Color : "#90CAF9",
                Status = Enum.IsDefined(source.Status) ? source.Status : NoteStatus.Inbox,
                Priority = source.Priority is >= 0 and <= 2 ? source.Priority : 0,
                DueAt = source.DueAt, ReminderEnabled = source.ReminderEnabled, ArchivedAt = source.ArchivedAt,
                ReminderSentAt = source.ReminderSentAt,
                ReminderSnoozedUntil = source.ReminderSnoozedUntil,
                CreatedAt = source.CreatedAt == default ? DateTime.UtcNow : source.CreatedAt,
                UpdatedAt = source.UpdatedAt == default ? DateTime.UtcNow : source.UpdatedAt,
                TagsJson = NormalizeArray(source.TagsJson), ChecklistJson = NormalizeArray(source.ChecklistJson)
            });
        }
        await _notes.ImportNotesAsync(imports);
        return imports.Count;
    }

    public async Task<NoteImportPreview> PreviewImportAsync(string path)
    {
        var document = await ReadDocumentAsync(path);
        var existing = await _notes.GetAllNotesAsync();
        var keys = existing.Select(note => Key(note.Title, note.Description)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newNotes = document.Notes!.Where(note => !string.IsNullOrWhiteSpace(note.Title) && keys.Add(Key(note.Title, note.Description))).ToList();
        return new NoteImportPreview(newNotes.Count, document.Notes.Count - newNotes.Count,
            string.Join(Environment.NewLine, newNotes.Take(5).Select(note => $"• {note.Title}")));
    }

    private static async Task<NoteExportDocument> ReadDocumentAsync(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length is <= 0 or > MaximumImportBytes) throw new InvalidDataException("Choose a JSON export smaller than 10 MB.");
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<NoteExportDocument>(stream, new JsonSerializerOptions { MaxDepth = 32 });
        if (document is null || document.Version != FormatVersion || document.Notes is null || document.Notes.Count > 10000)
            throw new InvalidDataException("This file is not a supported Kanban Cards export.");
        if (document.Notes.Any(note => note is null))
            throw new InvalidDataException("This export contains an invalid note entry.");
        return document;
    }

    private static string NormalizeArray(string? json)
    {
        return JsonSerializer.Serialize(ReadStringArray(json));
    }

    private static string[] ReadStringArray(string? json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json ?? "[]") ?? Array.Empty<string>(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }

    private static string Key(string title, string? description) => $"{title.Trim()}\n{description?.Trim()}";
    private static bool IsHexColor(string color) => color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit);

    private static async Task AtomicWriteAsync(string path, string contents)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(tempPath, contents, new UTF8Encoding(false));
            File.Move(tempPath, fullPath, true);
        }
        finally
        {
            try { File.Delete(tempPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

public sealed class NoteExportDocument
{
    public int Version { get; set; }
    public DateTime ExportedAt { get; set; }
    public System.Collections.Generic.List<Note> Notes { get; set; } = new();
}

public sealed record NoteImportPreview(int NewNotes, int Duplicates, string SampleTitles);

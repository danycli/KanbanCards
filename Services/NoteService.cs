using KanbanCards.Data;
using KanbanCards.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KanbanCards.Services;

public interface INoteService
{
    Task<List<Note>> GetAllNotesAsync();
    Task<Note?> GetNoteAsync(int id);
    Task<Note> CreateNoteAsync(Note note);
    Task<Note> ImportNoteAsync(Note note);
    Task ImportNotesAsync(IReadOnlyCollection<Note> notes);
    Task UpdateNoteAsync(Note note);
    Task UpdateNoteStatusAsync(int id, NoteStatus status);
    Task UpdateNotePinAsync(int id, bool isPinned);
    Task UpdateNoteArchiveAsync(int id, bool archived);
    Task ReorderNotesAsync(NoteStatus status, IReadOnlyList<int> orderedNoteIds);
    Task DeleteNoteAsync(int id);
    event Action NotesChanged;
}

public class NoteService : INoteService
{
    private readonly IDbContextFactory<KanbanCardsDbContext>? _contextFactory;
    private readonly KanbanCardsDbContext? _context;
    private readonly System.Threading.SemaphoreSlim _semaphore = new(1, 1);

    [ActivatorUtilitiesConstructor]
    public NoteService(IDbContextFactory<KanbanCardsDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    internal NoteService(KanbanCardsDbContext context)
    {
        _context = context;
    }

    public async Task<List<Note>> GetAllNotesAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_contextFactory != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Notes
                    .AsNoTracking()
                    .OrderByDescending(n => n.IsPinned)
                    .ThenBy(n => n.SortOrder)
                    .ThenByDescending(n => n.CreatedAt)
                    .ThenByDescending(n => n.Id)
                    .ToListAsync();
            }
            else
            {
                return await _context!.Notes
                    .AsNoTracking()
                    .OrderByDescending(n => n.IsPinned)
                    .ThenBy(n => n.SortOrder)
                    .ThenByDescending(n => n.CreatedAt)
                    .ThenByDescending(n => n.Id)
                    .ToListAsync();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Note?> GetNoteAsync(int id)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_contextFactory != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                return await context.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
            }
            else
            {
                return await _context!.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public event Action? NotesChanged;

    public async Task<Note> CreateNoteAsync(Note note)
    {
        await _semaphore.WaitAsync();
        try
        {
            note.CreatedAt = DateTime.UtcNow;
            note.UpdatedAt = note.CreatedAt;

            if (_contextFactory != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                context.Notes.Add(note);
                await context.SaveChangesAsync();
            }
            else
            {
                _context!.Notes.Add(note);
                await _context.SaveChangesAsync();
            }
        }
        finally
        {
            _semaphore.Release();
        }

        NotesChanged?.Invoke();
        return note;
    }

    public async Task<Note> ImportNoteAsync(Note note)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_contextFactory != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                context.Notes.Add(note);
                await context.SaveChangesAsync();
            }
            else
            {
                _context!.Notes.Add(note);
                await _context.SaveChangesAsync();
            }
        }
        finally { _semaphore.Release(); }
        NotesChanged?.Invoke();
        return note;
    }

    public async Task ImportNotesAsync(IReadOnlyCollection<Note> notes)
    {
        if (notes == null || notes.Count == 0)
            return;

        await _semaphore.WaitAsync();
        try
        {
            if (_contextFactory != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                context.Notes.AddRange(notes);
                await context.SaveChangesAsync();
            }
            else
            {
                _context!.Notes.AddRange(notes);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[NOTE IMPORT EXCEPTION] Failed to import {notes.Count} notes: {ex.Message}");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }

        NotesChanged?.Invoke();
    }

    public async Task UpdateNoteAsync(Note note)
    {
        await _semaphore.WaitAsync();
        bool updated = false;
        try
        {
            if (_contextFactory != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var storedNote = await context.Notes.FindAsync(note.Id);
                if (storedNote == null) return;
                CopyEditableFields(note, storedNote);
                await context.SaveChangesAsync();
                updated = true;
            }
            else
            {
                var storedNote = await _context!.Notes.FindAsync(note.Id);
                if (storedNote == null) return;
                CopyEditableFields(note, storedNote);
                await _context.SaveChangesAsync();
                updated = true;
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            FullScreenMonitorService.LogAudit($"[NOTE CONCURRENCY] Note ID {note.Id} was deleted or modified externally: {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[NOTE UPDATE EXCEPTION] Failed to update note ID {note.Id}: {ex.Message}");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }

        if (updated)
        {
            NotesChanged?.Invoke();
        }
    }

    public async Task UpdateNoteStatusAsync(int id, NoteStatus status)
    {
        await _semaphore.WaitAsync();
        bool updated = false;
        try
        {
            if (_contextFactory != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var storedNote = await context.Notes.FindAsync(id);
                if (storedNote == null || storedNote.Status == status) return;
                storedNote.Status = status;
                storedNote.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
                updated = true;
            }
            else
            {
                var storedNote = await _context!.Notes.FindAsync(id);
                if (storedNote == null || storedNote.Status == status) return;
                storedNote.Status = status;
                storedNote.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                updated = true;
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            FullScreenMonitorService.LogAudit($"[NOTE CONCURRENCY] Note ID {id} changed while its status was being updated: {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[NOTE STATUS EXCEPTION] Failed to update note ID {id}: {ex.Message}");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }

        if (updated)
            NotesChanged?.Invoke();
    }

    public async Task UpdateNotePinAsync(int id, bool isPinned)
    {
        await _semaphore.WaitAsync();
        bool updated = false;
        try
        {
            if (_contextFactory != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var storedNote = await context.Notes.FindAsync(id);
                if (storedNote == null || storedNote.IsPinned == isPinned) return;
                storedNote.IsPinned = isPinned;
                storedNote.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
                updated = true;
            }
            else
            {
                var storedNote = await _context!.Notes.FindAsync(id);
                if (storedNote == null || storedNote.IsPinned == isPinned) return;
                storedNote.IsPinned = isPinned;
                storedNote.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                updated = true;
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            FullScreenMonitorService.LogAudit($"[NOTE CONCURRENCY] Note ID {id} changed while its pin was being updated: {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[NOTE PIN EXCEPTION] Failed to update note ID {id}: {ex.Message}");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }

        if (updated)
            NotesChanged?.Invoke();
    }

    public async Task UpdateNoteArchiveAsync(int id, bool archived)
    {
        await _semaphore.WaitAsync();
        bool updated = false;
        try
        {
            if (_contextFactory != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var note = await context.Notes.FindAsync(id);
                if (note == null || note.IsArchived == archived) return;
                note.ArchivedAt = archived ? DateTime.UtcNow : null;
                note.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
                updated = true;
            }
            else
            {
                var note = await _context!.Notes.FindAsync(id);
                if (note == null || note.IsArchived == archived) return;
                note.ArchivedAt = archived ? DateTime.UtcNow : null;
                note.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                updated = true;
            }
        }
        finally { _semaphore.Release(); }
        if (updated) NotesChanged?.Invoke();
    }

    public async Task ReorderNotesAsync(NoteStatus status, IReadOnlyList<int> orderedNoteIds)
    {
        if (orderedNoteIds == null || orderedNoteIds.Count == 0)
            return;

        await _semaphore.WaitAsync();
        bool updated = false;
        try
        {
            if (_contextFactory != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var notes = await context.Notes
                    .Where(note => note.Status == status && orderedNoteIds.Contains(note.Id))
                    .ToListAsync();
                updated = ApplySortOrder(notes, orderedNoteIds);
                if (updated)
                    await context.SaveChangesAsync();
            }
            else
            {
                var notes = await _context!.Notes
                    .Where(note => note.Status == status && orderedNoteIds.Contains(note.Id))
                    .ToListAsync();
                updated = ApplySortOrder(notes, orderedNoteIds);
                if (updated)
                    await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[NOTE ORDER EXCEPTION] Failed to reorder notes in {status}: {ex.Message}");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }

        if (updated)
            NotesChanged?.Invoke();
    }

    private static bool ApplySortOrder(List<Note> notes, IReadOnlyList<int> orderedNoteIds)
    {
        var byId = notes.ToDictionary(note => note.Id);
        bool updated = false;
        for (int index = 0; index < orderedNoteIds.Count; index++)
        {
            if (byId.TryGetValue(orderedNoteIds[index], out var note) && note.SortOrder != index)
            {
                note.SortOrder = index;
                updated = true;
            }
        }
        return updated;
    }

    private static void CopyEditableFields(Note source, Note target)
    {
        var reminderScheduleChanged = target.DueAt != source.DueAt || target.ReminderEnabled != source.ReminderEnabled;
        target.Title = source.Title;
        target.Description = source.Description;
        target.Color = source.Color;
        target.Priority = source.Priority;
        target.DueAt = source.DueAt;
        target.ReminderEnabled = source.ReminderEnabled;
        if (reminderScheduleChanged)
        {
            target.ReminderSentAt = null;
            target.ReminderSnoozedUntil = null;
        }
        target.TagsJson = source.TagsJson;
        target.ChecklistJson = source.ChecklistJson;
        target.UpdatedAt = DateTime.UtcNow;
    }

    public async Task DeleteNoteAsync(int id)
    {
        await _semaphore.WaitAsync();
        bool deleted = false;
        try
        {
            if (_contextFactory != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var note = await context.Notes.FindAsync(id);
                if (note != null)
                {
                    context.Notes.Remove(note);
                    await context.SaveChangesAsync();
                    deleted = true;
                }
            }
            else
            {
                var note = await _context!.Notes.FindAsync(id);
                if (note != null)
                {
                    _context.Notes.Remove(note);
                    await _context!.SaveChangesAsync();
                    deleted = true;
                }
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            FullScreenMonitorService.LogAudit($"[NOTE CONCURRENCY] Note ID {id} was already removed: {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[NOTE DELETE EXCEPTION] Failed to delete note ID {id}: {ex.Message}");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }

        if (deleted)
        {
            NotesChanged?.Invoke();
        }
    }
}

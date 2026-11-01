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
    Task UpdateNoteAsync(Note note);
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
                    .OrderByDescending(n => n.CreatedAt)
                    .ThenByDescending(n => n.Id)
                    .ToListAsync();
            }
            else
            {
                return await _context!.Notes
                    .AsNoTracking()
                    .OrderByDescending(n => n.CreatedAt)
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

    public async Task UpdateNoteAsync(Note note)
    {
        await _semaphore.WaitAsync();
        bool updated = false;
        try
        {
            note.UpdatedAt = DateTime.UtcNow;

            if (_contextFactory != null)
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                context.Notes.Update(note);
                await context.SaveChangesAsync();
                updated = true;
            }
            else
            {
                _context!.Notes.Update(note);
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

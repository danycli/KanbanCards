using KanbanCards.Data;
using KanbanCards.Models;
using Microsoft.EntityFrameworkCore;
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
    private readonly KanbanCardsDbContext _context;

    public NoteService(KanbanCardsDbContext context)
    {
        _context = context;
    }

    public async Task<List<Note>> GetAllNotesAsync()
    {
        return await _context.Notes
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .ToListAsync();
    }

    public async Task<Note?> GetNoteAsync(int id)
    {
        return await _context.Notes.FindAsync(id);
    }

    public event Action? NotesChanged;

    public async Task<Note> CreateNoteAsync(Note note)
    {
        note.CreatedAt = DateTime.UtcNow;
        note.UpdatedAt = note.CreatedAt;
        _context.Notes.Add(note);
        await _context.SaveChangesAsync();
        NotesChanged?.Invoke();
        return note;
    }

    public async Task UpdateNoteAsync(Note note)
    {
        note.UpdatedAt = DateTime.UtcNow;
        _context.Notes.Update(note);
        await _context.SaveChangesAsync();
        NotesChanged?.Invoke();
    }

    public async Task DeleteNoteAsync(int id)
    {
        var note = await _context.Notes.FindAsync(id);
        if (note != null)
        {
            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
            NotesChanged?.Invoke();
        }
    }
}

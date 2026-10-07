# Kanban Cards — Development TODO

> A native Windows desktop sticky-note utility focused on fast, unobtrusive note capture.
>
> Product name: **Kanban Cards**
>
> Primary goal: create a lightweight note application that stays in the Windows system tray and represents notes as a compact, colorful stack on the right edge of the primary monitor. The user should be able to create, inspect, edit, search, and delete notes without living inside a conventional application window.

---

## 1. Product Vision

- [ ] Build **Kanban Cards** as a native-feeling Windows desktop utility.
- [ ] The application must stay out of the user's way when not actively being used.
- [ ] The application must not appear as a normal persistent taskbar application.
- [ ] The application must live in the Windows system tray when running in the background.
- [ ] A small transparent overlay on the right side of the primary monitor provides direct access to notes.
- [ ] Notes are represented by colored visual elements when collapsed.
- [ ] Hovering over the stack expands it enough to expose note titles.
- [ ] Clicking a note opens its editor.
- [ ] The main management window provides a complete searchable list of notes.
- [ ] The application must work completely offline.
- [ ] No account, backend, cloud service, or internet connection is required for the core product.

---

# 2. Core User Experience

## 2.1 First launch

- [ ] User launches Kanban Cards for the first time.
- [ ] Application starts.
- [ ] Application does not remain as a normal taskbar application.
- [ ] Application places its tray icon in the Windows notification area/system tray.
- [ ] Application starts its right-edge transparent overlay.
- [ ] If there are no notes, the overlay shows a `+` interaction element.
- [ ] The `+` must visually look like an available creation control, not like an existing sticky note.
- [ ] User can create the first note directly from the `+`.
- [ ] User can also open the tray menu and choose `Add Note`.

## 2.2 Normal running state

- [ ] Kanban Cards remains available through the system tray.
- [ ] The right-side stack/creation control remains visible while the application is running.
- [ ] The stack is positioned on the right side of the primary monitor.
- [ ] The stack is vertically centered.
- [ ] The stack never moves to a secondary monitor in v1.
- [ ] The stack does not require the main application window to remain open.

---

# 3. Technology Stack

## 3.1 Required stack

- [ ] Language: **C#**
- [ ] Runtime/framework: **.NET**
- [ ] UI framework: **WPF**
- [ ] Architecture: **MVVM**
- [ ] Database: **SQLite**
- [ ] ORM/data access: **Entity Framework Core**
- [ ] Platform: **Windows**
- [ ] System tray integration: Windows-compatible tray implementation
- [ ] Windows startup: proper Windows startup registration
- [ ] Local persistence only for v1

## 3.2 Architectural principles

- [ ] Keep UI, view models, business logic, persistence, and Windows integration separated.
- [ ] Avoid putting database logic directly inside WPF code-behind.
- [ ] Avoid tightly coupling the edge overlay to the main window.
- [ ] Use services for Windows-specific functionality.
- [ ] Keep note ordering deterministic.
- [ ] Keep all core note operations testable without requiring the UI.
- [ ] Avoid unnecessary dependencies.
- [ ] Do not introduce a backend or web server.
- [ ] Do not introduce cloud synchronization in v1.
- [ ] Do not introduce authentication in v1.
- [ ] Do not introduce AI features in v1.
- [ ] Do not add unrelated productivity features merely because they are easy to implement.

---

# 4. Suggested Project Structure

```text
KanbanCards/
├── App.xaml
├── App.xaml.cs
│
├── Models/
│   └── Note.cs
│
├── Data/
│   ├── KanbanCardsDbContext.cs
│   ├── Migrations/
│   └── Repositories/
│
├── Services/
│   ├── NoteService.cs
│   ├── AutoSaveService.cs
│   ├── TrayService.cs
│   ├── StartupService.cs
│   ├── StackService.cs
│   ├── WindowPositionService.cs
│   └── SettingsService.cs
│
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── NoteEditorViewModel.cs
│   ├── SettingsViewModel.cs
│   └── NoteListItemViewModel.cs
│
├── Views/
│   ├── MainWindow.xaml
│   ├── NoteEditorWindow.xaml
│   ├── SettingsWindow.xaml
│   └── EdgeStackWindow.xaml
│
├── Resources/
│   ├── Styles/
│   ├── Colors/
│   ├── Icons/
│   └── Animations/
│
└── Tests/
```

The exact structure may be adjusted if the implementation benefits from another clean MVVM organization, but responsibilities must remain separated.

---

# 5. Note Data Model

Each note must contain:

```text
Note
├── Id
├── Title
├── Description
├── Color
├── CreatedAt
└── UpdatedAt
```

## 5.1 Id

- [ ] Every note must have a unique identifier.
- [ ] Prefer a database-generated integer ID or GUID.
- [ ] The ID must remain stable for the lifetime of the note.

## 5.2 Title

- [ ] Title is mandatory.
- [ ] A note cannot be saved as a normal persistent note with an empty title.
- [ ] Title validation must be handled cleanly in the UI.
- [ ] Whitespace-only titles should be treated as empty.
- [ ] The title may be longer than the title shown in the stack.
- [ ] The stack display must truncate long titles.

## 5.3 Description

- [ ] Description is optional.
- [ ] Description supports multi-line text.
- [ ] Description must be searchable.
- [ ] Description must be persisted.
- [ ] Description should support normal text editing behavior.

## 5.4 Color

- [ ] Every note has a color.
- [ ] New notes default to **Blue**.
- [ ] User can change the color.
- [ ] The selected color must be persisted.
- [ ] The same color must be reflected in the note's edge-stack representation.
- [ ] Color must also be visible in relevant note-list/editor UI.

Initial palette:

```text
Blue
Yellow
Green
Red
Purple
Orange
```

The architecture should make it easy to add additional colors later.

## 5.5 CreatedAt

- [ ] Record when the note was initially created.
- [ ] Creation timestamp must never be overwritten during ordinary edits.
- [ ] CreatedAt determines chronological stack ordering.

## 5.6 UpdatedAt

- [ ] Update when note content changes.
- [ ] Update after successful persistence.
- [ ] Editing a note must NOT move it to the top of the stack.

---

# 6. SQLite Persistence

- [ ] Use SQLite for local persistence.
- [ ] Use Entity Framework Core.
- [ ] Database should be created/migrated automatically as appropriate.
- [ ] Database should survive application restarts.
- [ ] Database should be stored in an appropriate per-user application-data location.
- [ ] Do not store the database beside the executable if that creates permission/update problems.
- [ ] Notes must remain available after Windows restarts.
- [ ] Notes must remain available after the application exits and restarts.
- [ ] Database operations must be asynchronous where appropriate.
- [ ] Handle database errors gracefully.
- [ ] Do not silently lose note content if persistence fails.
- [ ] Log useful diagnostic information for unexpected persistence failures.

---

# 7. Autosave

Autosave is a core requirement.

Required behavior:

```text
User types
    ↓
Change detected
    ↓
Debounce approximately 300–500 ms
    ↓
Save to SQLite
```

## 7.1 Autosave requirements

- [ ] Autosave title changes.
- [ ] Autosave description changes.
- [ ] Autosave color changes.
- [ ] Do not write to disk on every individual keystroke.
- [ ] Use a debounce mechanism.
- [ ] Target debounce interval: approximately 300–500 ms.
- [ ] Prevent overlapping saves for the same note.
- [ ] Ensure the latest pending change is persisted before the editor closes.
- [ ] Handle rapid typing safely.
- [ ] Handle rapid edits safely.
- [ ] Handle cancellation/disposal of pending debounce operations.
- [ ] Avoid race conditions where an older save overwrites a newer save.
- [ ] Existing notes should be persisted continuously while being edited.

## 7.2 New note autosave

- [ ] New notes may initially exist as an unsaved draft.
- [ ] Title is required before creating the persistent note.
- [ ] Once sufficient information exists to persist the note, save it.
- [ ] The first successful persistence creates the note ID and CreatedAt.
- [ ] Deleting an unsaved new note should discard it immediately without confirmation.

---

# 8. Main Window

The main management window should follow this visual concept:

```text
┌─────────────────────────────────────┐
│ My Notes                       − X  │
├─────────────────────────────────────┤
│ 🔍 Search notes...                  │
│                                     │
│ 🟨 Assignment                       │
│ 🟦 Project idea                     │
│ 🟩 Buy something                    │
│ 🟪 Meeting                          │
│                                     │
│                              [+]    │
└─────────────────────────────────────┘
```

## 8.1 Main window behavior

- [ ] Window title/branding: **My Notes** or appropriate Kanban Cards branding.
- [ ] Window should have standard minimize/close controls as appropriate.
- [ ] Main window is not required to remain open.
- [ ] Closing the main window must not terminate the background application.
- [ ] Closing the main window should leave Kanban Cards running in the tray.
- [ ] The edge stack remains available after the main window closes.
- [ ] Main window can be reopened through the tray menu.
- [ ] Main window can be opened through `View Notes`.
- [ ] `+` creates a new note.
- [ ] Notes appear in creation order, newest first.
- [ ] The main list must be scrollable if required.
- [ ] Main window should remain usable with many notes.

---

# 9. Search

Search must cover both:

- [ ] Note title
- [ ] Note description

## 9.1 Search behavior

- [ ] Search updates as the user types.
- [ ] Search should be case-insensitive.
- [ ] Search should handle partial matches.
- [ ] Search should match title text.
- [ ] Search should match description text.
- [ ] Search results must preserve the normal newest-first ordering.
- [ ] Search must not modify note order.
- [ ] Clearing the search restores the complete note list.
- [ ] Empty search shows all notes.
- [ ] Search should remain responsive with a large number of notes.

Example:

```text
Search: networking

🟨 CN Assignment
   Finish networking assignment

🟦 Router Project
   Configure the networking lab
```

---

# 10. Add Note

The `+` button must create a new note.

The editor should visually follow this concept:

```text
┌───────────────────────────┐
│ New Note              X   │
├───────────────────────────┤
│ Color: 🟨 🟦 🟩 🟥 🟪 🟧 │
│                           │
│ Title                     │
│ ┌───────────────────────┐ │
│ │                       │ │
│ └───────────────────────┘ │
│                           │
│ Description               │
│ ┌───────────────────────┐ │
│ │                       │ │
│ │                       │ │
│ └───────────────────────┘ │
│                           │
│              Delete   OK  │
└───────────────────────────┘
```

## 10.1 Editor fields

- [ ] Color selector.
- [ ] Title field.
- [ ] Description field.
- [ ] Delete button.
- [ ] OK button.
- [ ] X close button.
- [ ] Blue is selected by default.
- [ ] Title is required.
- [ ] Description is optional.

---

# 11. OK and X behavior

For an existing or successfully persisted note:

- [ ] Clicking OK saves/flushes pending changes.
- [ ] Clicking OK closes the editor.
- [ ] Clicking X saves/flushes pending changes.
- [ ] Clicking X closes the editor.
- [ ] The note remains in the right-side stack.
- [ ] The note remains in the main notes list.
- [ ] The note can be reopened later.
- [ ] Closing the editor must not delete the note.
- [ ] Clicking elsewhere on the desktop must NOT close the editor.

For a new note:

- [ ] Validate required title.
- [ ] Persist required content before closing.
- [ ] Then close the editor.
- [ ] Add/update the edge-stack representation.

---

# 12. Delete Behavior

## 12.1 Existing note

- [ ] Clicking Delete on an existing note opens a confirmation dialog.
- [ ] Confirmation dialog must clearly state that the note will be permanently deleted.
- [ ] Provide Cancel.
- [ ] Provide Delete.
- [ ] Cancel leaves the note unchanged.
- [ ] Delete permanently removes the note from SQLite.
- [ ] Remove it from the main list.
- [ ] Remove it from the edge stack.
- [ ] Close the editor after successful deletion.

Suggested dialog:

```text
Delete note?

This note will be permanently deleted.

Cancel          Delete
```

## 12.2 Unsaved new note

- [ ] If the note has never been persisted, Delete immediately discards it.
- [ ] No confirmation dialog is required.
- [ ] Close the editor.
- [ ] Do not create a database record.

---

# 13. Editor Positioning

When a note is opened from the right-side stack:

- [ ] Open the editor near the stack.
- [ ] Preserve spatial relationship with the selected note.
- [ ] Never allow the editor to extend beyond the visible primary monitor.
- [ ] If there is insufficient space on the right, position it farther left.
- [ ] If there is insufficient vertical space, clamp the position to the monitor bounds.
- [ ] Account for Windows DPI scaling.
- [ ] Account for different screen resolutions.
- [ ] Account for taskbar/work-area boundaries where appropriate.
- [ ] Do not move the editor to another monitor in v1.

---

# 14. Right-Side Edge Overlay

The edge overlay is one of the defining features of Kanban Cards.

## 14.1 General behavior

- [ ] Transparent/borderless window.
- [ ] Positioned on the right side of the primary monitor.
- [ ] Vertically centered.
- [ ] Always available while Kanban Cards is running.
- [ ] Does not behave like a normal application window.
- [ ] Does not appear as a normal taskbar application.
- [ ] Should not unnecessarily interfere with desktop interaction.
- [ ] Should use Windows-compatible transparent window behavior.
- [ ] Should be layered independently from the main application window.

## 14.2 Empty state

When there are zero notes:

```text
                         ┌───┐
                         │ + │
                         └───┘
```

- [ ] Display a plus icon on the right edge.
- [ ] Put the plus inside a transparent overlay window.
- [ ] It should visually communicate "add a note".
- [ ] It must not look like an existing sticky note.
- [ ] Clicking it opens a new note editor.
- [ ] The overlay remains available after note creation.

---

# 15. Collapsed Note Stack

When notes exist, each note is represented by a colored collapsed element.

Conceptually:

```text
                              ██
                              ██
                              ██
                              ██
```

## Dimensions

Collapsed:

- [ ] Approximately 20–30 px wide.
- [ ] Height should be visually appropriate to the chosen design.
- [ ] Individual notes must remain distinguishable.
- [ ] Use the note's selected color.
- [ ] Use subtle shadows/depth where appropriate.

## Ordering

- [ ] Newest note appears at the top.
- [ ] Oldest visible note appears toward the bottom.
- [ ] Never randomize note order.
- [ ] Never alphabetize the edge stack.
- [ ] Editing a note does not move it.
- [ ] CreatedAt determines ordering.

---

# 16. Expanded Edge Stack

When the cursor enters the stack:

```text
Mouse enters
    ↓
150–200 ms delay
    ↓
Stack expands
```

Expanded width:

- [ ] Approximately 220–260 px.

Concept:

```text
┌────────────────────┐
│ Assignment      🟨 │
├────────────────────┤
│ Project idea    🟦 │
├────────────────────┤
│ Buy something   🟩 │
├────────────────────┤
│ Meeting         🟪 │
└────────────────────┘
```

## Expanded behavior

- [ ] Display note titles.
- [ ] Preserve creation order.
- [ ] Show newest notes at the top.
- [ ] Use the note color as part of the visual identity.
- [ ] Keep the stack visually compact.
- [ ] Avoid expanding farther than necessary.

---

# 17. Title Truncation

The edge-stack title display has a strict truncation rule.

- [ ] Maximum displayed length: 20 characters.
- [ ] If title length is 20 characters or less, display it normally.
- [ ] If title is longer than 20 characters, display the first 17 characters followed by `...`.
- [ ] Result is exactly 20 displayed characters.

Example:

```text
Very long networking...
```

Do not use arbitrary truncation lengths in different components.

The main window and editor may display the complete title.

---

# 18. Hover Exit

When the cursor leaves the expanded stack:

```text
Mouse leaves
    ↓
200 ms delay
    ↓
Stack collapses
```

- [ ] Delay must be approximately 200 ms.
- [ ] Cancel the collapse if the cursor returns during the delay.
- [ ] Prevent flickering.
- [ ] Use subtle animation.
- [ ] Do not close any open note editor merely because the cursor leaves the stack.

---

# 19. Edge Stack Scrolling

The edge stack has a maximum visible count of **10 notes**.

## 19.1 Initial state

If there are more than 10 notes:

- [ ] Show the newest 10 notes.
- [ ] Newest note is at the top.
- [ ] The 10th visible note is the oldest currently displayed item.

Example:

```text
Note 20
Note 19
Note 18
Note 17
Note 16
Note 15
Note 14
Note 13
Note 12
Note 11
```

## 19.2 Mouse wheel

One mouse-wheel scroll should shift the visible window by one note.

Example after one downward scroll:

```text
Note 19
Note 18
Note 17
Note 16
Note 15
Note 14
Note 13
Note 12
Note 11
Note 10
```

After another:

```text
Note 18
Note 17
Note 16
Note 15
Note 14
Note 13
Note 12
Note 11
Note 10
Note 9
```

Requirements:

- [ ] Only 10 notes remain visible.
- [ ] One wheel movement advances the viewport by one note.
- [ ] Older notes enter from the bottom.
- [ ] The top visible note disappears as the viewport advances.
- [ ] Do not skip multiple notes for a single normal wheel step.
- [ ] Prevent accidental scrolling from moving the viewport unpredictably.
- [ ] Handle the beginning/end bounds correctly.

## 19.3 Reset after cursor leaves

After the user has scrolled the stack and then moves the cursor away:

- [ ] Stack collapses.
- [ ] Scroll viewport resets to the newest notes.
- [ ] Next time the stack expands, it starts from the newest 10 notes.
- [ ] Do not preserve an old scrolled position between hover sessions.

---

# 20. Maximum Stack Height

- [ ] Maximum expanded height should be approximately 60–70% of the primary monitor height.
- [ ] Never allow the stack to extend beyond the usable monitor area.
- [ ] If there are fewer than 10 notes, size appropriately.
- [ ] If there are more than 10 notes, keep exactly 10 visible in the viewport.
- [ ] The stack must remain usable at smaller screen resolutions.
- [ ] Account for DPI scaling.

---

# 21. Primary Monitor Only

- [ ] Edge stack appears only on the primary monitor.
- [ ] Main editor positioning is also restricted to the primary monitor for v1.
- [ ] Secondary monitors must not receive duplicate stacks.
- [ ] Do not create one overlay per monitor.
- [ ] Use Windows APIs to reliably determine the primary monitor/work area.
- [ ] Handle display configuration changes gracefully.

---

# 22. Animations

Use subtle, polished animations.

Required animated interactions:

- [ ] Stack expansion.
- [ ] Stack collapse.
- [ ] Note opening.
- [ ] Note closing.
- [ ] Note creation.
- [ ] Note deletion.
- [ ] Appropriate hover transitions.
- [ ] Avoid excessive animation.
- [ ] Animation should reinforce spatial relationships.
- [ ] Respect the Settings animation toggle.

Animations should be fast and unobtrusive.

Suggested principle:

```text
Interaction
    ↓
small movement/fade/scale
    ↓
settle quickly
```

Do not use dramatic animations.

---

# 23. Visual Design

Primary aesthetic:

**Sticky-note aesthetic: colorful cards, subtle shadows, playful.**

## 23.1 Design principles

- [ ] Notes should visually resemble physical sticky notes without becoming skeuomorphic clutter.
- [ ] Colors should be cheerful and distinct.
- [ ] Shadows should be subtle.
- [ ] Corners can be softly rounded.
- [ ] Typography should remain clean and readable.
- [ ] UI should feel lightweight.
- [ ] Avoid excessive gradients.
- [ ] Avoid excessive glassmorphism.
- [ ] Avoid visually heavy dashboards.
- [ ] Maintain good contrast.
- [ ] Keep the application recognizable as a note utility.

## 23.2 Color system

Initial note colors:

- Blue
- Yellow
- Green
- Red
- Purple
- Orange

- [ ] Centralize color definitions.
- [ ] Do not hardcode colors throughout individual controls.
- [ ] Ensure readable text contrast for every note color.
- [ ] The exact visual shades can be refined during the polish phase.

---

# 24. System Tray

The application must provide a system tray icon.

Tray behavior:

```text
Tray icon
   ↓
Click
   ↓
Menu
```

Menu:

```text
┌────────────────────┐
│ Add Note            │
│ View Notes          │
│ Settings            │
├────────────────────┤
│ Exit                │
└────────────────────┘
```

## 24.1 Add Note

- [ ] Opens a new note editor.

## 24.2 View Notes

- [ ] Opens the main notes window.

## 24.3 Settings

- [ ] Opens Settings.

## 24.4 Exit

- [ ] Completely terminates the application.
- [ ] Stops tray integration.
- [ ] Removes edge overlay.
- [ ] Flushes pending saves.
- [ ] Performs clean shutdown.

---

# 25. Taskbar Behavior

- [ ] Background application must not maintain a normal taskbar entry.
- [ ] Main management window may appear while open.
- [ ] When the main window is closed/minimized according to the chosen behavior, the application remains available through the tray.
- [ ] The edge overlay must not create an unwanted taskbar button.
- [ ] The note editor should not create unwanted permanent taskbar entries.
- [ ] Use appropriate WPF/Windows window styles.

---

# 26. Windows Startup

- [ ] Kanban Cards must support starting with Windows.
- [ ] Use proper Windows startup registration.
- [ ] Do not depend on a crude manual Startup-folder shortcut unless necessary.
- [ ] Startup should launch the application silently.
- [ ] Application should initialize in tray/background mode.
- [ ] The main window must not automatically obstruct the user's desktop after login.
- [ ] Edge stack should become available after startup.
- [ ] User should be able to disable startup from Settings.
- [ ] Startup setting must persist.

---

# 27. Settings

Initial Settings:

```text
☑ Start with Windows
☑ Launch minimized to tray
☑ Show notes on desktop
☑ Enable animations

Stack position:
○ Right
○ Left

Animation speed:
──────●──────
```

## 27.1 Start with Windows

- [ ] Toggle Windows startup integration.
- [ ] Persist setting.
- [ ] Reflect actual startup configuration where possible.

## 27.2 Launch minimized to tray

- [ ] Default enabled.
- [ ] Application starts without opening the main window.

## 27.3 Show notes on desktop

- [ ] Toggle edge stack visibility.
- [ ] When disabled, the application may remain in the tray.
- [ ] Re-enable restores the stack.

## 27.4 Enable animations

- [ ] Default enabled.
- [ ] Disabling should remove/reduce transition animations.
- [ ] Functional behavior must remain unchanged.

## 27.5 Stack position

- [ ] Default: Right.
- [ ] Optional Left position may be supported by the architecture.
- [ ] Current product requirement is right side.
- [ ] If Left is implemented, all edge-stack interaction should mirror appropriately.

## 27.6 Animation speed

- [ ] Provide a simple user-adjustable animation speed control.
- [ ] Avoid excessive complexity.
- [ ] Persist the selected speed.

---

# 28. Empty State

When there are zero notes:

- [ ] Do not show fake note cards.
- [ ] Show a `+` on the right edge.
- [ ] The plus should be inside the transparent overlay.
- [ ] Clicking `+` opens the New Note editor.
- [ ] The main notes window should also provide `+`.
- [ ] The main window should display a useful empty state.

Suggested main-window empty state:

```text
No notes yet

Create your first note with +
```

The exact wording can be refined during UI polish.

---

# 29. Note Lifecycle

The intended lifecycle:

```text
Create
  ↓
Draft
  ↓
Title provided
  ↓
Persist
  ↓
Autosave
  ↓
OK/X
  ↓
Collapsed edge note
  ↓
Hover
  ↓
Expanded title
  ↓
Click
  ↓
Edit
  ↓
Autosave
  ↓
OK/X
  ↓
Back to stack
```

Deletion:

```text
Existing note
    ↓
Delete
    ↓
Confirmation
    ↓
Confirm
    ↓
SQLite deletion
    ↓
Remove from UI
```

New unsaved note:

```text
New draft
    ↓
Delete
    ↓
Immediate discard
```

---

# 30. Window and Overlay Requirements

For the edge overlay:

- [ ] Borderless.
- [ ] Transparent background.
- [ ] No standard window chrome.
- [ ] No taskbar button.
- [ ] Correct hit testing.
- [ ] Mouse interactions should work only where the visual controls exist.
- [ ] Transparent portions should not unnecessarily block desktop interaction.
- [ ] Always-on-top behavior should be carefully chosen.
- [ ] Overlay must not permanently steal keyboard focus.
- [ ] Overlay should not interfere with normal application usage.
- [ ] Overlay should survive normal main-window closing.
- [ ] Overlay should be destroyed during application exit.

---

# 31. Accessibility and Usability

- [ ] Keyboard navigation should work in the main window.
- [ ] Title field should be easy to focus.
- [ ] Description field should support normal keyboard editing.
- [ ] Buttons should have accessible labels/tooltips where appropriate.
- [ ] Color choices should have textual labels in addition to visual color.
- [ ] Do not rely solely on color to distinguish note actions.
- [ ] Ensure sufficient text contrast.
- [ ] Respect reasonable Windows text scaling/DPI settings.
- [ ] Avoid tiny click targets despite the compact visual design.

---

# 32. Error Handling

- [ ] Database failure must not silently discard user content.
- [ ] Failed saves should be surfaced appropriately.
- [ ] Application startup errors should be logged.
- [ ] Tray initialization errors should be handled.
- [ ] Windows startup registration errors should be handled.
- [ ] Invalid note state should be validated.
- [ ] Unexpected exceptions should not silently terminate the entire application where recovery is possible.
- [ ] Avoid intrusive error dialogs for recoverable background failures.
- [ ] Provide useful diagnostic logs during development.

---

# 33. Logging

- [ ] Add structured application logging.
- [ ] Log startup/shutdown.
- [ ] Log database initialization/migration.
- [ ] Log unexpected database failures.
- [ ] Log tray initialization failures.
- [ ] Log startup-registration failures.
- [ ] Do not log sensitive user note content unnecessarily.
- [ ] Do not log full note descriptions by default.

---

# 34. Performance

The application is intended to remain lightweight.

- [ ] Low idle CPU usage.
- [ ] Low memory usage.
- [ ] No continuous high-frequency polling if event-driven behavior is possible.
- [ ] Use mouse/window events rather than constantly polling screen coordinates.
- [ ] Debounce autosave.
- [ ] Do not reload the entire database for every keystroke.
- [ ] Avoid recreating the entire stack unnecessarily.
- [ ] Keep animations efficient.
- [ ] Use virtualization where useful for large main-window note lists.
- [ ] Ensure scrolling remains smooth.

---

# 35. Multi-Monitor and Display Handling

v1:

- [ ] Primary monitor only.
- [ ] No duplicate stacks on secondary monitors.

Still handle:

- [ ] Monitor resolution changes.
- [ ] Windows display scaling changes.
- [ ] Application startup after monitor changes.
- [ ] Primary monitor changing while application is running.
- [ ] Work-area changes due to taskbar configuration.
- [ ] Recalculate overlay position when necessary.

---

# 36. Data Integrity

- [ ] Never use display order as the permanent ordering source.
- [ ] Always use CreatedAt and a deterministic tie-breaker such as Id.
- [ ] Do not modify CreatedAt when editing.
- [ ] Update UpdatedAt when content changes.
- [ ] Ensure deletes are actually persisted.
- [ ] Ensure pending autosaves are flushed before application shutdown.
- [ ] Avoid stale in-memory notes overwriting newer database state.
- [ ] Keep UI and database state synchronized.

---

# 37. Testing Requirements

## 37.1 Note creation

- [ ] Create a note.
- [ ] Verify default color is Blue.
- [ ] Verify title is required.
- [ ] Verify description is optional.
- [ ] Verify note appears at the top.
- [ ] Verify note survives application restart.

## 37.2 Autosave

- [ ] Type title.
- [ ] Type description.
- [ ] Pause.
- [ ] Restart application.
- [ ] Verify latest content remains.
- [ ] Test rapid typing.
- [ ] Test rapid edits.
- [ ] Test closing immediately after typing.
- [ ] Verify pending changes are flushed.

## 37.3 Editing

- [ ] Open note from stack.
- [ ] Change title.
- [ ] Change description.
- [ ] Change color.
- [ ] Click X.
- [ ] Reopen note.
- [ ] Verify changes remain.
- [ ] Verify note position/order did not change.

## 37.4 Deleting

- [ ] Delete existing note.
- [ ] Verify confirmation appears.
- [ ] Cancel deletion.
- [ ] Verify note remains.
- [ ] Confirm deletion.
- [ ] Verify note disappears from stack.
- [ ] Verify note disappears from main list.
- [ ] Verify note is absent after restart.
- [ ] Delete unsaved new note.
- [ ] Verify no confirmation appears.

## 37.5 Stack

- [ ] Verify collapsed state.
- [ ] Verify expanded state.
- [ ] Verify 150–200 ms hover delay.
- [ ] Verify 200 ms exit delay.
- [ ] Verify newest note is top.
- [ ] Verify title truncation.
- [ ] Verify correct note colors.
- [ ] Verify stack remains centered vertically.
- [ ] Verify stack remains on primary monitor.

## 37.6 More than 10 notes

- [ ] Create at least 11 notes.
- [ ] Verify only 10 are initially visible.
- [ ] Verify newest 10 are shown.
- [ ] Scroll once.
- [ ] Verify viewport shifts by exactly one note.
- [ ] Scroll again.
- [ ] Verify another one-note shift.
- [ ] Scroll toward oldest notes.
- [ ] Verify bounds.
- [ ] Leave cursor.
- [ ] Verify stack resets to newest 10.
- [ ] Re-enter stack.
- [ ] Verify newest 10 are visible again.

## 37.7 Search

- [ ] Search title.
- [ ] Search description.
- [ ] Search partial text.
- [ ] Search with different letter casing.
- [ ] Clear search.
- [ ] Verify all notes return.
- [ ] Verify search does not modify ordering.

## 37.8 Tray

- [ ] Start application.
- [ ] Verify tray icon.
- [ ] Verify no persistent taskbar entry.
- [ ] Open tray menu.
- [ ] Test Add Note.
- [ ] Test View Notes.
- [ ] Test Settings.
- [ ] Test Exit.

## 37.9 Startup

- [ ] Enable Start with Windows.
- [ ] Restart Windows.
- [ ] Verify application starts.
- [ ] Verify it launches silently.
- [ ] Verify tray icon appears.
- [ ] Verify edge stack appears.
- [ ] Disable startup.
- [ ] Verify it no longer launches automatically.

## 37.10 Display

- [ ] Test 100% scaling.
- [ ] Test higher DPI scaling.
- [ ] Test different resolutions.
- [ ] Test secondary monitor.
- [ ] Verify stack remains on primary monitor.
- [ ] Verify editor stays within primary monitor bounds.

---

# 38. Visual Polish Checklist

- [ ] Sticky-note aesthetic.
- [ ] Colorful cards.
- [ ] Subtle shadows.
- [ ] Consistent corner radius.
- [ ] Clean typography.
- [ ] Comfortable spacing.
- [ ] Smooth hover transitions.
- [ ] Smooth opening/closing.
- [ ] Polished buttons.
- [ ] Clear destructive-action styling.
- [ ] Visually obvious `+` control.
- [ ] Color selector is clear.
- [ ] Search field is visually integrated.
- [ ] Empty state is clean.
- [ ] No unnecessary visual clutter.

---

# 39. Security and Privacy

- [ ] Notes remain local.
- [ ] No network calls are required for core functionality.
- [ ] No user account is required.
- [ ] No note content is uploaded anywhere.
- [ ] Do not collect telemetry in v1 unless explicitly added later.
- [ ] Do not expose the SQLite database through a network service.
- [ ] Avoid logging note contents.
- [ ] Follow normal Windows file-permission practices.

---

# 40. What Is Explicitly Out of Scope for v1

Do NOT add these unless requirements change:

- [ ] Cloud synchronization.
- [ ] User accounts.
- [ ] Login.
- [ ] Backend server.
- [ ] Mobile application.
- [ ] Browser extension.
- [ ] AI-generated notes.
- [ ] AI summaries.
- [ ] Reminders.
- [ ] Calendar integration.
- [ ] Collaboration.
- [ ] Sharing.
- [ ] Real-time synchronization.
- [ ] Attachments.
- [ ] Image storage.
- [ ] Markdown editor.
- [ ] Rich text editor.
- [ ] Tags.
- [ ] Categories.
- [ ] Kanban boards despite the name.
- [ ] Complex task-management features.

The name is **Kanban Cards**, but the v1 product behavior remains the lightweight sticky-note system defined above.

---

# 41. Development Phases

## Phase 1 — Project Foundation

- [ ] Verify installed .NET SDK.
- [ ] Create WPF project.
- [ ] Establish MVVM architecture.
- [ ] Establish dependency injection if appropriate.
- [ ] Establish configuration/settings infrastructure.
- [ ] Establish logging.
- [ ] Establish project folders.
- [ ] Establish basic application lifecycle.

## Phase 2 — Persistence

- [ ] Add SQLite.
- [ ] Add EF Core.
- [ ] Create Note entity.
- [ ] Create DbContext.
- [ ] Create migrations.
- [ ] Implement repository/data access.
- [ ] Implement NoteService.
- [ ] Test CRUD.

## Phase 3 — Main Window

- [ ] Build My Notes window.
- [ ] Build note list.
- [ ] Build search.
- [ ] Build `+`.
- [ ] Build empty state.
- [ ] Implement newest-first ordering.

## Phase 4 — Note Editor

- [ ] Build editor.
- [ ] Build title field.
- [ ] Build description field.
- [ ] Build color selector.
- [ ] Set Blue as default.
- [ ] Build OK.
- [ ] Build X.
- [ ] Build Delete.
- [ ] Add title validation.
- [ ] Add delete confirmation.

## Phase 5 — Autosave

- [ ] Implement debounce.
- [ ] Implement background persistence.
- [ ] Flush pending changes on close.
- [ ] Test rapid typing.
- [ ] Test shutdown.

## Phase 6 — System Tray

- [ ] Implement tray icon.
- [ ] Implement tray menu.
- [ ] Remove unwanted taskbar behavior.
- [ ] Implement Add Note.
- [ ] Implement View Notes.
- [ ] Implement Settings.
- [ ] Implement Exit.

## Phase 7 — Edge Overlay

- [ ] Build transparent overlay.
- [ ] Position on primary monitor.
- [ ] Center vertically.
- [ ] Implement empty-state `+`.
- [ ] Implement collapsed colored note elements.

## Phase 8 — Stack Interaction

- [ ] Implement hover delay.
- [ ] Implement expansion.
- [ ] Implement title display.
- [ ] Implement 20-character truncation.
- [ ] Implement hover animations.
- [ ] Implement collapse delay.
- [ ] Prevent flicker.

## Phase 9 — Scrolling

- [ ] Implement 10-note viewport.
- [ ] Implement one-note wheel movement.
- [ ] Implement oldest/newest boundaries.
- [ ] Implement scroll reset after cursor leaves.
- [ ] Test with 11+ notes.
- [ ] Test with many notes.

## Phase 10 — Editor Integration

- [ ] Open editor from stack.
- [ ] Position near selected note.
- [ ] Keep editor open when clicking elsewhere.
- [ ] Keep editor within monitor bounds.
- [ ] Synchronize changes with stack.

## Phase 11 — Settings

- [ ] Start with Windows.
- [ ] Launch minimized to tray.
- [ ] Show notes on desktop.
- [ ] Enable animations.
- [ ] Animation speed.
- [ ] Stack position architecture.

## Phase 12 — Polish

- [ ] Apply sticky-note aesthetic.
- [ ] Refine colors.
- [ ] Refine shadows.
- [ ] Refine spacing.
- [ ] Refine animations.
- [ ] Refine typography.
- [ ] Refine hover states.
- [ ] Refine empty states.
- [ ] Refine dialogs.

## Phase 13 — Integration Testing

- [ ] Test complete user flow.
- [ ] Test startup.
- [ ] Test tray.
- [ ] Test main window.
- [ ] Test editor.
- [ ] Test autosave.
- [ ] Test stack.
- [ ] Test scrolling.
- [ ] Test deletion.
- [ ] Test search.
- [ ] Test display scaling.
- [ ] Test multiple monitors.

## Phase 14 — Release

- [ ] Release build.
- [ ] Verify executable.
- [ ] Verify database initialization.
- [ ] Verify startup registration.
- [ ] Verify clean uninstall behavior.
- [ ] Package Windows installer.
- [ ] Test installation.
- [ ] Test upgrade/update path if applicable.
- [ ] Test clean uninstall.
- [ ] Produce release documentation.

---

# 42. AI Coding Agent Rules

These rules apply to every coding-agent prompt.

- [ ] Inspect the existing project before making changes.
- [ ] Do not rewrite working components unnecessarily.
- [ ] Do not change the architecture without a concrete reason.
- [ ] Preserve existing functionality.
- [ ] Keep changes focused on the requested phase.
- [ ] Build after significant changes.
- [ ] Run tests after significant changes.
- [ ] Fix compilation errors before moving on.
- [ ] Do not leave placeholder implementations disguised as finished functionality.
- [ ] Do not silently remove requirements.
- [ ] Do not invent product behavior without documenting the decision.
- [ ] Keep code readable.
- [ ] Use meaningful names.
- [ ] Avoid unnecessary abstractions.
- [ ] Avoid giant classes.
- [ ] Keep Windows-specific logic isolated in services where practical.
- [ ] Keep database operations outside UI code-behind.
- [ ] Do not add dependencies unless justified.
- [ ] Document important architectural decisions.
- [ ] Verify actual behavior rather than assuming compilation means success.
- [ ] Before modifying an existing feature, inspect its current implementation.
- [ ] After implementation, report files changed and tests performed.
- [ ] Do not proceed to unrelated features when the requested phase is incomplete.

---

# 43. Definition of Done

Kanban Cards v1 is considered complete when:

- [ ] It runs as a Windows desktop application.
- [ ] It starts in the system tray.
- [ ] It does not maintain an unwanted taskbar presence.
- [ ] It can start with Windows.
- [ ] It displays the right-side edge interaction.
- [ ] It shows `+` when there are zero notes.
- [ ] It creates notes.
- [ ] Title is mandatory.
- [ ] Description is optional.
- [ ] Blue is the default color.
- [ ] User can choose note colors.
- [ ] Notes autosave with approximately 300–500 ms debounce.
- [ ] OK saves and closes.
- [ ] X saves and closes.
- [ ] Existing-note Delete requires confirmation.
- [ ] Unsaved new-note Delete immediately discards.
- [ ] Notes persist in SQLite.
- [ ] Notes appear newest-first.
- [ ] Editing does not change creation order.
- [ ] Stack is centered on the right side of the primary monitor.
- [ ] Stack expands after approximately 150–200 ms hover delay.
- [ ] Stack collapses approximately 200 ms after mouse exit.
- [ ] Expanded stack displays titles.
- [ ] Long titles display 17 characters + `...`.
- [ ] Maximum of 10 notes are visible at once.
- [ ] Mouse-wheel scrolling shifts the visible viewport by one note.
- [ ] Scrolling resets to newest notes after leaving the stack.
- [ ] Clicking a stack note opens its editor.
- [ ] Editor opens near the stack.
- [ ] Editor stays within the primary monitor.
- [ ] Clicking outside the editor does not close it.
- [ ] Search covers title and description.
- [ ] Tray menu contains Add Note, View Notes, Settings, and Exit.
- [ ] Main window provides the searchable complete note list.
- [ ] Settings provide the agreed initial controls.
- [ ] Animations are subtle and configurable.
- [ ] Application remains lightweight and offline.
- [ ] Core tests pass.
- [ ] Release build works.
- [ ] Installation and uninstallation work correctly.

---

# 44. Future Ideas — Not Part of v1

These are deliberately recorded for possible later versions, but must not be implemented during v1 unless explicitly requested:

- [ ] Tags.
- [ ] Categories.
- [ ] Note pinning.
- [ ] Note priorities.
- [ ] Reminders.
- [ ] Keyboard shortcuts.
- [ ] Global hotkeys.
- [ ] Drag-and-drop note reordering.
- [ ] Multiple edge stacks.
- [ ] Left/right/bottom placement.
- [ ] Multiple monitor support.
- [ ] Cloud synchronization.
- [ ] Android companion application.
- [ ] Web companion.
- [ ] Export/import.
- [ ] Backup/restore.
- [ ] Rich text.
- [ ] Markdown.
- [ ] Images.
- [ ] Attachments.
- [ ] Search filters.
- [ ] Archived notes.
- [ ] Trash/recycle bin.
- [ ] Themes.
- [ ] Dark mode.
- [ ] Custom note colors.
- [ ] Custom stack sizes.
- [ ] Custom animations.
- [ ] Accessibility enhancements.
- [ ] Analytics/telemetry.
- [ ] AI-assisted note organization.

---

# 45. Product Principle

Kanban Cards should remain simple.

The user should be able to:

```text
See
 ↓
Create
 ↓
Write
 ↓
Forget about it
 ↓
Find it later
 ↓
Edit
 ↓
Delete
```

without opening a large productivity application.

The edge stack is the defining interaction.

The tray keeps the application out of the way.

The main window provides management.

SQLite provides persistence.

Autosave prevents accidental loss.

The visual design provides the sticky-note identity.

Everything else is secondary.

# KanbanCards feature audit and implementation roadmap

**Audit date:** 11 November 2026
**Scope:** Current product behavior, update and installer path, and feature opportunities that fit the app's quiet, bezel-docked sticky-card design.
**Basis:** Source review of the WPF app, view models, EF Core/SQLite model, setup project, packaging scripts, and existing tests. This is a source audit; no release was built or published as part of it.

## Executive summary

KanbanCards already has a distinctive capture surface: notes can live at the screen edge, open into a small editor, and remain available in a searchable board and tray app. At audit time, the board was a single chronological list. The features in this roadmap have now been implemented in the working tree and packaged as local version 1.4.0.

The first product step was a **small, calm workflow board**: four user-understandable lanes (Inbox, Next, Doing, Done), with status selection on each card. Existing cards enter Inbox automatically. The second step adds pinning and drag reordering within each lane. The edge stack remains a glanceable surface, showing active cards and keeping completed cards out of the way by default.

Before shipping product changes, the update path needs a release pass. The app and setup can deliver a new build, but version values are repeated across projects and installer definitions, and silent setup currently applies fresh-install defaults during upgrades. That can change a user's Windows startup preference. The release process should make versioning and upgrade behavior explicit and verify a real upgrade before publishing.

### Implementation status (11 November 2026, v1.4.0)

- **Workflow lanes:** Implemented in the working tree. Existing notes migrate to Inbox; the board now has four lanes, global search, per-card status selection, and active-only edge-stack behavior.
- **Pinning and manual order:** Implemented in the working tree. Cards can be pinned to the top and dragged to reorder within a lane; order is persisted and reordering is paused while search filters the visible lane.
- **Product features:** Features 1–12 below are implemented: lanes, pin/reorder, archive/restore, priority, due dates/reminders, checklists, tags/filters, global quick capture, JSON/Markdown transfer and backups, templates, editor/accessibility refinements, and clipboard capture.
- **Release plumbing:** Central app/setup version is 1.4.0; silent setup targets the existing install directory, preserves current startup/shortcut presence, and returns failure status. The updater only relaunches after a successful setup exit. A self-contained setup bundle was built locally and its SHA-256 verified.
- **Verification performed:** All 109 automated tests pass. Live isolated-profile checks confirmed note metadata persistence and archive/restore behavior, including the corrected dynamic Restore action. A synthetic legacy database migrated through all four new migrations with its original note preserved and a pre-migration backup created. Card metadata and the editor were visually inspected. Installer verification passes against the rebuilt v1.4.0 bundle.
- **Still required before public release:** Exercise an in-place upgrade with a copy of an actual earlier-release database, refresh the board screenshot assets, and publish the verified setup plus `SHA256SUMS` to a GitHub release. No external release was published from this workspace.

## 1. What the app does today

| Area | Existing behavior | Source evidence |
|---|---|---|
| Edge cards | Up to ten notes appear in a left- or right-docked staircase; scrolling pages through larger collections. Hover expands the stack and card selection opens the editor. | `EdgeStackViewModel`, `EdgeStackWindow`, `MotionCoordinator` |
| Note editing | Title, free-form description, and one of six pastel colors; existing notes autosave after a short pause. New blank drafts are not committed until confirmed. | `Note`, `NoteEditorViewModel`, `NoteEditorWindow` |
| Board | Search matches title and description and is debounced; notes are displayed in reverse-created order in one list. | `MainViewModel`, `MainWindow` |
| Removal | Delete asks for confirmation for saved notes. | `NoteEditorViewModel`, `ConfirmDeleteDialog` |
| Preferences | Startup, launch-to-tray, desktop visibility, animations/speed, docking side, and automatic update checks. | `SettingsViewModel`, `SettingsWindow`, `SettingsService` |
| Windows integration | Tray menu, single-instance activation, optional startup entry, per-monitor fullscreen suppression, and editor placement around the active display. | `TrayService`, `SingleInstanceService`, `StartupService`, `FullScreenMonitorService`, `EditorService` |
| Storage | Local SQLite database and JSON settings in the user's Local AppData. Existing note schema has no status, tags, due date, or manual sort position. | `KanbanCardsDbContext`, `Note`, initial migration |
| Updates | Checks GitHub's latest public release, presents release notes, downloads an installer, verifies available integrity/authenticity evidence, silently launches setup, closes the app, and attempts to relaunch it. | `UpdateService`, `UpdateNoticeViewModel`, setup project |

### Product gaps found

1. Screenshot assets still show the previous flat list and should be refreshed before public release.
2. The legacy `.iss` recipe retains separate version metadata; the supported native setup path is versioned from `Directory.Build.props`.

## 2. Recommended feature roadmap

These items record the original recommendations and implementation approach. Each feature below is implemented in v1.4.0.

### Feature 1 — Workflow lanes: Inbox, Next, Doing, Done

**Status:** Implemented in v1.4.0.

**Why:** This closes the gap between the KanbanCards name and the current flat list, while giving notes a useful lifecycle without making the app feel like a project-management suite.

**Implementation approach:**

- Add a persisted `Status` enum/string to `Note`; migration assigns all existing rows to `Inbox`.
- Replace the board's single list with four compact columns/cards views, using the same rounded white cards, pastel accents, and restrained shadows already in `MainWindow.xaml`.
- Support drag-and-drop between lanes and a keyboard-accessible status menu on each card. Dragging must not be the only way to change state.
- Keep search global across lanes and show each lane's result count. Preserve current selection and scroll position while the filtered collections reconcile.
- Keep the edge stack focused on Inbox/Next/Doing by default; Done remains visible in the board and can be revealed in the stack through a preference if user research supports it.
- When a user completes a card, use a subtle visual state and avoid celebratory animation or persistent clutter.

**Data and compatibility:** Add an EF Core migration with a safe default; old databases upgrade in place. Keep `CreatedAt` and `UpdatedAt` semantics clear: status changes update `UpdatedAt`.

**Acceptance checks:** Existing notes open unchanged; status survives restart; drag and keyboard move cards; search finds cards in every lane; empty lanes have quiet, helpful prompts; high-DPI and narrow-window layouts remain usable.

### Feature 2 — Manual ordering and a small “keep at top” affordance

**Status:** Implemented in v1.4.0.

**Why:** Creation order is a weak proxy for what the user needs now. Manual ordering makes both board and edge stack more intentional.

**Implementation approach:** Add a persisted `SortOrder` (or per-lane order) and provide drag reorder within a lane. Use a small pin icon to keep a few cards at the top, with a soft limit or clear visual treatment so it does not become a second priority system. Retain a “Recently updated” sort option for users who prefer the current automatic behavior. Keep the edge stack order consistent with the chosen rule.

**Data and compatibility:** Existing cards receive deterministic positions based on current ordering. Reorder updates should be batched and not create a visible autosave pause.

**Acceptance checks:** Order is stable across restart and search; moving a card between lanes inserts it predictably; pinned cards are visibly identifiable but do not dominate the pastel card design.

### Feature 3 — Archive and restore

**Status:** Implemented in v1.4.0.

**Why:** Done is a workflow state; archive is how users keep old reference cards without letting them crowd the board or edge stack.

**Implementation approach:** Add an `ArchivedAt` nullable timestamp. Add an unobtrusive archive action and an Archive view/filter in the board. Archived notes remain searchable when the Archive filter is selected, stay out of the edge stack, and can be restored with one action. Keep permanent deletion behind the existing confirmation dialog.

**Data and compatibility:** A nullable timestamp is an additive migration. Do not silently purge archived cards.

**Acceptance checks:** Archive/restore preserves title, description, color, and timestamps; archived notes cannot accidentally appear in the active edge stack; delete remains distinct from archive.

### Feature 4 — Priority markers

**Status:** Implemented in v1.4.0.

**Why:** Users need a quick way to distinguish urgent work without relying on card color, which already serves as a calm visual category.

**Implementation approach:** Add a compact priority value (`Normal`, `Important`, `Urgent`) with a subtle glyph and accessible text. Keep color independent. Allow sort/filter by priority and let urgent active cards rise in the edge stack only if the user enables that behavior.

**Data and compatibility:** Default old notes to `Normal`; keep enum values stable for database compatibility.

**Acceptance checks:** Priority is readable without color perception; changing priority does not overwrite palette color; default display stays quiet for normal cards.

### Feature 5 — Due dates and gentle reminders

**Status:** Implemented in v1.4.0.

**Why:** Some sticky notes are reminders, but the app currently has no time-based action or indication.

**Implementation approach:** Add an optional local due date/time and a reminder toggle. Start with a single reminder per card, surfaced through the existing Windows notification area/tray integration. Add “Today” and “Upcoming” filters and a small due indicator on cards. Do not add recurring schedules until one-shot reminders prove useful.

**Data and compatibility:** Persist a timezone-aware or clearly local due timestamp; handle sleep/wake and missed notifications by showing one catch-up notification, not a burst. Old notes have no due date.

**Acceptance checks:** Reminder fires once, respects disabled notifications, survives restart, and has a clear snooze/dismiss path. Test DST/time-zone changes and past-due items.

### Feature 6 — Checklists inside a note

**Status:** Implemented in v1.4.0.

**Why:** A short task often needs a few steps, but the existing editor is a single free-form description field.

**Implementation approach:** Add an optional checklist section below the description, with small checkbox rows, inline entry, and completion count. Keep the description field untouched for existing users and avoid converting plain text automatically. The edge stack should show a compact “2/4” count only when a checklist exists.

**Data and compatibility:** Store checklist items as normalized child rows or a versioned JSON value; use a child table if item-level ordering and future reminders are expected. Migration must preserve the existing text exactly.

**Acceptance checks:** Reorder, check/uncheck, and edit items; autosave and close do not lose the last keystroke; keyboard-only entry and checkbox access work.

### Feature 7 — Tags and quick filters

**Status:** Implemented in v1.4.0.

**Why:** Users may want lightweight grouping such as Work, Personal, or Ideas without creating many boards.

**Implementation approach:** Add user-created tags with small muted chips in the editor and board; support filtering by one or more tags alongside search. Keep the six card colors as visual tone, not as an implicit tag taxonomy. Start with simple tags rather than folders or nested projects.

**Data and compatibility:** Use a normalized Note-to-Tag relation if tags are independently managed; migration leaves existing notes untagged.

**Acceptance checks:** Tag filtering composes with text search, deleted tags do not orphan notes, and chips wrap cleanly at narrow widths.

### Feature 8 — Quick capture from anywhere

**Status:** Implemented in v1.4.0.

**Why:** The edge stack is already a fast capture surface, but users must interact with the edge or open the board.

**Implementation approach:** Add a configurable global hotkey that opens a small, focused quick-capture card without raising the full board. Default to title plus optional one-line detail; save to Inbox. Register/unregister safely, handle hotkey conflicts, and provide a tray fallback. Do not intercept common typing shortcuts.

**Data and compatibility:** Use the existing note service and database path. Add only the hotkey preference; no new note schema is needed beyond workflow status.

**Acceptance checks:** Works while another app is focused; conflicting hotkeys show a useful settings message; quick capture does not steal focus after save unless requested.

### Feature 9 — Export, import, and backup

**Status:** Implemented in v1.4.0.

**Why:** Notes are personal data stored locally. Users need a straightforward escape hatch before upgrades, migrations, or device changes.

**Implementation approach:** Add a board-level Export action to JSON and human-readable Markdown; add a validated import preview with duplicate handling. Add an automatic rotating database backup before schema migration, with a restore action in settings. Keep backup files local and clearly explain their location.

**Data and compatibility:** Version the export format; write via temporary file then atomic replace. Validate size and schema before importing. Backups must include SQLite WAL state safely (use SQLite backup API or close/checkpoint) rather than copying only the main database file while writes are active.

**Acceptance checks:** Round-trip notes preserve Unicode, colors, dates, statuses, and checklist/tag data; invalid imports leave the current database untouched; backup restore has a confirmation and pre-restore safety copy.

### Feature 10 — Reusable templates

**Status:** Implemented in v1.4.0.

**Why:** Repeated meeting notes, daily plans, and bug reports can be captured faster with a small structure.

**Implementation approach:** Offer a few built-in optional templates in the new-card flow, using the existing title/description fields (and checklist when available). Allow a user to save a note as a template later. Keep template choice lightweight and skippable; don't impose a form on ordinary notes.

**Data and compatibility:** Store templates separately from notes so they do not appear in search, the board, or edge stack.

**Acceptance checks:** Creating from a template makes an ordinary editable note; changing a template does not change existing notes.

### Feature 11 — Better editor ergonomics and accessibility

**Status:** Implemented in v1.4.0.

**Why:** The app's quiet visual style is strongest when it is quick and usable without a mouse.

**Implementation approach:** Add explicit accessible names and keyboard focus order to palette choices and editor actions; offer an editor font-size preference and a “compact/comfortable” board density toggle; keep current rounded geometry, warm pastel colors, and short easing. Add focus-visible outlines that match the indigo accent.

**Data and compatibility:** Preferences only; no note migration.

**Acceptance checks:** Keyboard-only create/edit/save/delete; high contrast and Windows text scaling remain legible; resizing does not clip controls.

### Feature 12 — Windows clipboard capture

**Status:** Implemented in v1.4.0.

**Why:** Users often want to turn a selected snippet into a note without retyping it.

**Implementation approach:** Add a tray command and optional hotkey to create a note from the current clipboard text, with a compact confirmation preview. Preserve multiline text in the description and generate a conservative title from the first non-empty line. Never monitor the clipboard continuously.

**Data and compatibility:** Uses current note persistence; avoid new data permissions/settings unless the hotkey is configurable.

**Acceptance checks:** Empty clipboard is handled; large clipboard content is bounded with an explanation; clipboard content is only read on explicit user action.

## 3. Update and release path audit — do this before distributing feature changes

### Current update flow

1. `App.OnStartup` starts the background updater. The first check is delayed; subsequent checks use a six-hour interval if automatic checks are enabled.
2. `UpdateService` calls GitHub's latest-release API, ignores drafts/prereleases, parses the version tag, and compares it with the app assembly version.
3. An available release is presented in the update notice. Dismissed versions are remembered; manual checks can show them again.
4. The updater selects a setup `.exe` asset, preferring `KanbanCardsSetup.exe`; it downloads to a temp folder over HTTPS.
5. It checks a published SHA-256 file when available. If the running app has a trusted Authenticode signer, it requires the downloaded setup to have the same signer.
6. It launches setup with `/SILENT /NORESTART`, starts a PowerShell watcher to relaunch the installed app after setup, and then shuts down the current app.
7. The current `Installer\build.ps1` builds a self-contained setup host with the main app payload embedded in `app.zip`; silent setup extracts files, registers the install, and optionally starts the app.

### Release-readiness findings

**P1 — Silent upgrades can change startup preference. [Implemented]** Silent setup now detects an existing install, carries forward the Windows Run entry and existing shortcut presence, and receives the installed directory from the updater. Existing install path, startup entry, and shortcut choices are preserved by the current code path.

**P1 — Version is duplicated in multiple places. [Implemented for the supported path]** Main app and setup project now share `Directory.Build.props` at 1.4.0, and setup registry metadata reads the setup assembly version. The legacy `.iss` recipe remains outside the supported release path and must be updated if that path is reactivated.

**P1 — The update service and custom setup handoff disagree in comments and assumptions. [Code aligned; end-to-end check pending]** The stale Inno-specific handoff comment is removed. The updater passes `/DIR`, `/SILENT`, and `/NORESTART` to the native WPF setup; a watcher checks the setup exit code before relaunching. A real upgrade still needs to be exercised before public distribution.

**P1 — Silent installer errors can look like success to the updater. [Implemented]** Silent setup now exits non-zero after a caught install failure. The relaunch watcher waits for setup completion and starts the app only after a zero exit code.

**P2 — Checksum verification is optional and the checksum is fetched from the same release.** HTTPS protects transport, but a checksum hosted alongside the binary is not an independent trust anchor. Current signature matching only enforces publisher identity when the running executable is already signed. For a durable release chain, sign app and setup builds with a stable publisher certificate and verify it as mandatory; keep SHA-256 as corruption detection and publish checksums for every downloadable artifact.

**P2 — Installer definitions do not all reflect the current install model.** The active custom setup defaults to `%LocalAppData%\Programs\KanbanCards`; the maintained `.iss` script targets `{autopf}` and hard-kills the app. The README describes the custom setup, while the updater's old comments refer to Inno. Clearly mark one supported release path and either bring the alternate script into parity or remove it from the supported release workflow.

**P2 — The updater has no explicit rollback/health confirmation.** If installation is interrupted or the new app fails to start, there is no built-in recovery prompt. Before adding rollback machinery, first make setup atomic (stage payload, replace, retain previous files until success) and confirm launch health on next startup.

### Recommended update work before a feature release

1. Completed: upgrade-mode settings preservation and installer success/failure exit codes.
2. Completed: a central version source for app/setup metadata and release label.
3. Completed: the custom WPF installer is the canonical release path; the legacy Inno script is not used by the build.
4. Completed: updater comments and installer documentation match the active setup.
5. Automated feature and data-transfer coverage is present; broader installer fault-injection cases remain future hardening.
6. Local v1.4.0 bundle built and checksum verified. Before public release, install over an actual previous release and confirm settings/data survive, then exercise in-app update and relaunch on a clean Windows test machine.
7. Release notes name the user-visible changes and database migration; publish the release and retain the prior installer for recovery.

## 4. Implementation sequence and current status

### Release plumbing first

The P1 updater/installer fixes are complete in the working tree. The app keeps its data path stable, creates a pre-migration backup, migrates restored backups to the current schema, and rolls back the restore if that migration fails.

### First product release — a calm workflow board

Features 1–12 are included in v1.4.0, with their implementation details and acceptance checks recorded above.

### Follow-up releases

No roadmap feature remains unimplemented from this audit. The next quality pass should focus on reminder timing across Windows sleep/wake and daylight-saving changes, plus an in-place update from an actual previously released installer before public distribution.

## 5. Product design guardrails

- Keep the edge surface glanceable: short labels, a small active set, and no dense metadata.
- Preserve the six pastel colors as visual comfort and user choice; do not overload color to encode status, priority, and category at once.
- Use rounded cards, restrained indigo accents, soft shadows, and brief eased motion already present in the app.
- Make every important action available by keyboard as well as pointer.
- Keep capture fast and allow users to leave notes unstructured.
- Treat local notes as user data: additive migrations, automatic pre-migration backup, export, and no silent deletion.
- Prefer optional, quiet notifications. Avoid noisy badges, streaks, gamification, and always-on clipboard monitoring.

## 6. Source references

- Product overview and stated experience: `README.md`
- Note schema and display properties: `Models/Note.cs`
- SQLite configuration and schema: `Data/KanbanCardsDbContext.cs`, `Data/Migrations/`
- Board filtering and ordering: `ViewModels/MainViewModel.cs`, `MainWindow.xaml`
- Edge stack selection and paging: `ViewModels/EdgeStackViewModel.cs`, `Views/EdgeStackWindow.xaml`
- Editor and autosave: `ViewModels/NoteEditorViewModel.cs`, `Views/NoteEditorWindow.xaml`
- Settings persistence: `ViewModels/SettingsViewModel.cs`, `Services/SettingsService.cs`
- Update detection/download/handoff: `Services/UpdateService.cs`, `Services/Authenticode.cs`, `Models/UpdateInfo.cs`
- Setup behavior: `src/KanbanCards.Setup/App.xaml.cs`, `src/KanbanCards.Setup/Services/InstallerEngine.cs`
- Release packaging: `Installer/build.ps1`, `build-installer.ps1`, `Installer/KanbanCards.iss`
- Existing automated coverage: `tests/KanbanCards.Tests/`

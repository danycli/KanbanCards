# Release Notes - KanbanCards v1.2.0

*Release Date: October 23, 2026*  
*Previous Version: v1.1.0*

---

### Highlights & Key Improvements

- **MotionCoordinator Service**: Added a centralized, token-based animation coordinator to prevent UI stutter and eliminate stale animation callbacks during rapid user interaction.
- **Hardware-Accelerated Motion System**: Implemented pre-allocated and frozen Cubic and Quadratic easing functions with dynamic speed scaling based on user settings.
- **Smart Note Editor Auto-Positioning**: Programmed the Note Editor window to automatically center vertically and position itself adjacent to the active edge dock without obstructing the card stack.
- **EF Core Read Optimization**: Integrated `.AsNoTracking()` into note retrieval queries to significantly reduce memory allocation and Garbage Collection overhead.
- **Robust Viewport Bounds & Scroll Clamping**: Added explicit slot-indexed card tracking and dynamic scroll offset bounds to prevent indexing errors and UI drift when deleting notes.
- **Deterministic Scroll State Feedback**: Refactored edge stack scrolling to return status flags that eliminate redundant layout passes when scroll limits are reached.
- **Seamless Installer Upgrade & Lifecycle Handling**: Upgraded the Inno Setup script with LZMA2/max compression, x64 architecture matching, and automated process termination before installation or removal.
- **Automated Test Suite**: Added a dedicated xUnit test project covering application logic, viewport scrolling, motion math, and concurrent animation coordination.
- **Project Structure Optimization**: Isolated unit test projects from release builds in project configuration to maintain lean distribution artifacts.
- **Updated Single-File Executable**: Rebuilt self-contained 64-bit binaries and installer package containing all motion, editor, and database improvements.

---

### Artifacts

- **Installer Setup**: `Installer/Output/KanbanCardsSetup.exe`
- **Standalone Executable**: `bin/Release/net8.0-windows/win-x64/publish/KanbanCards.exe`

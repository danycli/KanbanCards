<div align="center">
  # KanbanCards

  **A modern, edge-docked sticky notes application for Windows, designed to keep your thoughts organized without cluttering your workspace.**

  ![Windows](https://img.shields.io/badge/OS-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)
  ![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
  ![WPF](https://img.shields.io/badge/Framework-WPF-5C2D91?style=for-the-badge)
  ![SQLite](https://img.shields.io/badge/Database-SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)
  ![Version](https://img.shields.io/badge/Version-1.1.0-6366F1?style=for-the-badge)

  ---
</div>

## What is KanbanCards?

**KanbanCards** is a sleek, non-intrusive productivity tool that lives along the edges of your screen. Instead of floating chaotically across your desktop, your notes stack into a subtle, collapsed "staircase" that peeks out just enough to remind you of your tasks.

Hovering over the edge expands the cards smoothly, allowing you to quickly read, edit, or create notes. It also features a dedicated Main Board window with live search and note management.

## Features

- **Customizable Edge Docking**: Dock your notes staircase to either the **Left** or **Right** edge of your screen. Corners and animations dynamically adjust to match the screen bezel.
- **Fluid Hover Animations**: Cards glide smoothly into view on hover with configurable animation speed and cubic easing.
- **Main Board & Live Search**: Full-featured note management window with instant search filtering, note counter badges, and relative date timestamps.
- **Color-Coded Organization**: Assign 6 distinct pastel card colors (Blue, Yellow, Green, Red, Purple, Orange) to visually group and prioritize tasks.
- **Distraction-Free Note Editor**: Frameless, draggable note cards with a refined header, non-colliding palette swatches, and minimalist close buttons.
- **Modern Modal Experience**: Custom in-app dialogs, including a branded delete confirmation popup and an elevated Settings window with backdrop dimming.
- **Fluent Settings Menu**: Customize startup behavior, tray minimization, desktop overlay, dock side, animation speed, and toggles via clean iOS/Fluent-style switches and segmented tabs.
- **Compact System Tray Integration**: Lightweight, native-feeling tray menu with Fluent glyphs for quick actions without clutter.
- **Persistent Local Storage**: Data is safely auto-saved locally using SQLite and Entity Framework Core.
- **Windows Installer**: Ready-to-use Inno Setup installer package with automatic desktop shortcuts and startup registration.

## Getting Started

### Prerequisites

- Windows 10 or Windows 11 (64-bit)
- [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (included in self-contained release builds)

### Installation

The easiest way to install KanbanCards is using the setup wizard:

1. Download or locate `KanbanCardsSetup.exe` in `Installer/Output/` (or compile `Installer/KanbanCards.iss` with Inno Setup).
2. Run the installer and follow the setup prompts.
3. Once installed, launch KanbanCards from your Start Menu or Desktop shortcut.

### Building from Source

To build and run the project locally:

```powershell
# Clone the repository
git clone https://github.com/yourusername/KanbanCards.git
cd KanbanCards

# Build the solution
dotnet build

# Run the application
dotnet run
```

To generate a self-contained, single-file release executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## How to Use

1. **Create a Note**: Hover over your configured screen edge (Right or Left) to expand the staircase, then click the **`+`** card, or use the **`+`** button in the Main Board window.
2. **Edit a Note**: Click any card in the stack or double-click a note in the Main Board to open the editor.
3. **Customize Colors**: Select one of the 6 color circles in the editor header to instantly restyle the note.
4. **Delete a Note**: Click **Delete** in the editor. A smooth confirmation dialog will appear to prevent accidental removal.
5. **Search Notes**: Open the Main Board window to search across note titles and content in real time.
6. **Adjust Settings**: Right-click the KanbanCards icon in the Windows notification area (system tray) or click the gear icon in the Main Board to change dock position, toggle animations, or adjust speed.
7. **Keyboard Shortcuts**: Press `Esc` to close the Settings dialog or Note Editor at any time.

## Tech Stack

- **C# / .NET 8** - Core application logic and runtime.
- **WPF (Windows Presentation Foundation)** - Frameless hardware-accelerated UI and animations.
- **Entity Framework Core & SQLite** - Local database storage and schema migrations.
- **H.NotifyIcon.Wpf** - Win32 system tray lifecycle and taskbar integration.
- **Inno Setup** - Windows installer compiler.

## Contributing

Contributions, issues, and feature requests are welcome. Feel free to open an issue or pull request on GitHub.

## License

This project is licensed under the [MIT License](LICENSE).

---

<div align="center">
  <i>Built to keep your workspace clean and your mind focused.</i>
</div>

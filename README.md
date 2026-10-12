<div align="center">
  <img src="https://via.placeholder.com/150/5C2D91/FFFFFF?text=KanbanCards" alt="KanbanCards Logo" width="120" height="120" />

  # 🗂️ KanbanCards

  **A modern, edge-docked sticky notes application for Windows, designed to keep your thoughts organized without cluttering your workspace.**

  ![Windows](https://img.shields.io/badge/OS-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)
  ![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
  ![WPF](https://img.shields.io/badge/Framework-WPF-5C2D91?style=for-the-badge)
  ![SQLite](https://img.shields.io/badge/Database-SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)

  ---
</div>

## 🌟 What is KanbanCards?

**KanbanCards** is a sleek, non-intrusive productivity tool that lives exactly where you need it: tucked away on the right edge of your screen. Instead of floating randomly across your desktop, your notes stack into a beautiful, collapsed "staircase" that peeks out just enough to remind you they're there. 

Hover over the edge, and the stack smoothly slides out, ready for you to read, edit, or add new ideas!

## ✨ Features

- 📐 **Edge-Docked Staircase Design**: Notes rest in a collapsed, layered stack on the edge of your screen. 
- 🪄 **Smooth Hover Animations**: Slide in and out of view effortlessly with fluid WPF animations—no layout snapping!
- 🎨 **Color-Coded Organization**: Assign beautiful pastel colors to individual notes to categorize your tasks visually.
- 💾 **Persistent SQLite Storage**: Never lose a thought. Everything is auto-saved locally in a lightweight SQLite database.
- 🖱️ **Draggable Editor**: Click a note to open its editor, and drag the editor anywhere on your screen.
- 🌙 **System Tray Integration**: Quietly runs in the background. Access settings, manage startup behavior, and quit straight from your taskbar tray.
- 📦 **One-Click Installer**: Comes with a proper `Inno Setup` Windows installer (`.exe`) to easily get it onto your system.

## 🚀 Getting Started

### Prerequisites

- Windows 10 or Windows 11
- [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (If running from source)

### Installation

The easiest way to get KanbanCards on your system is to use the compiled setup installer:

1. Locate the `KanbanCardsSetup.exe` installer inside the `Installer/Output` directory (or generate it by compiling `Installer/KanbanCards.iss` with Inno Setup).
2. Run the installer and follow the wizard.
3. Once installed, KanbanCards will automatically add a shortcut to your desktop and optionally launch on Windows startup!

### Building from Source

If you want to tweak the code yourself:

```powershell
# Clone the repository
git clone https://github.com/yourusername/KanbanCards.git
cd KanbanCards

# Build the project
dotnet build

# Run the app
dotnet run
```

To create a single-file portable executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 🎮 How to Use

1. **Add a Note**: Hover over the right edge of your screen to expand the stack, then click the **`+`** icon on the bottom card.
2. **Edit a Note**: Left-click on any expanded note to open the Note Editor.
3. **Change Colors**: Inside the editor, pick from 6 different pastel themes (Blue, Yellow, Green, Red, Purple, Orange) at the top.
4. **Delete a Note**: Click the **Delete** button in the bottom-left corner of the editor.
5. **Settings**: Right-click the purple KanbanCards logo in your Windows System Tray (bottom right of your taskbar) and click **Settings**.

## 🛠️ Tech Stack

- **C# / .NET 8** - Core application logic and runtime.
- **WPF (Windows Presentation Foundation)** - UI framework and fluid animations.
- **Entity Framework Core (SQLite)** - Database ORM for local persistent storage.
- **H.NotifyIcon.Wpf** - Modern system tray and taskbar management.
- **Inno Setup** - Windows installer compilation.

## 🤝 Contributing

Contributions, issues, and feature requests are always welcome! Feel free to check the [issues page](#) if you want to contribute.

## 📄 License

This project is open-source and available under the [MIT License](LICENSE).

---
<div align="center">
  <i>Built with ❤️ to keep your workspace clean and your mind focused.</i>
</div>

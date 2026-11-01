# KanbanCards Installer

This directory contains the packaging tooling and build scripts for the **KanbanCards** installer and uninstaller.

---

## Overview

KanbanCards features a native Windows setup application designed to match the clean card aesthetic of the main application. It requires zero administrative privileges (no UAC prompts), installs per-user, and acts as both the installation wizard and the clean uninstaller.

### Key Characteristics
- **Zero-UAC Deployment**: Installs to `%LocalAppData%\Programs\KanbanCards` by default, eliminating security barriers and enterprise privilege issues.
- **Payload Embedding**: The self-contained payload archive (`app.zip`) is dynamically compiled and packaged directly into `KanbanCardsSetup.exe`.
- **Integrated Uninstaller**: Copies itself as `uninstall.exe` to the destination directory and registers with Windows *Apps & Features* (Add/Remove Programs).
- **Safe Data Retention**: When uninstalled, application binaries and shortcuts are removed cleanly, while the user's note database (`%LocalAppData%\KanbanCards\KanbanCards.db`) is safely preserved.

---

## Command-Line Arguments

The installer and uninstaller support automated, unattended deployments:

| Flag | Description |
| :--- | :--- |
| `/SILENT` | Runs the setup without showing the interactive window. |
| `/VERYSILENT` | Suppresses all prompts and runs completely headless. |
| `/NORESTART` | Ensures no reboot requests are made during installation. |
| `/DIR="<Path>"` | Overrides the default install destination directory. |
| `/UNINSTALL` | Triggers the uninstaller workflow instead of the setup flow. |

---

## Directory Structure

```
Installer/
├── build.ps1               # Builds the modern native setup bundle (KanbanCardsSetup.exe)
├── verify.ps1              # Validates installer hash against SHA256SUMS
├── KanbanCards.iss         # Alternative Inno Setup configuration (for traditional MSI/EXE packaging)
├── README.md               # Installer documentation
└── Output/
    ├── KanbanCardsSetup.exe# Compiled native setup & uninstaller executable
    └── SHA256SUMS          # SHA-256 integrity checksum
```

---

## Building the Installer

To compile a fresh installer bundle:

```powershell
# From the repository root or the Installer directory:
powershell -ExecutionPolicy Bypass -File .\Installer\build.ps1
```

To build a fully self-contained installer (bundling the .NET 8 runtime):

```powershell
powershell -ExecutionPolicy Bypass -File .\Installer\build.ps1 -SelfContained
```

---

## Verifying Integrity

To verify the generated executable against the recorded SHA-256 hash:

```powershell
powershell -ExecutionPolicy Bypass -File .\Installer\verify.ps1
```

---

## Alternative: Inno Setup

For legacy enterprise environments requiring traditional Inno Setup packaging, `KanbanCards.iss` is also maintained and compatible with Inno Setup 6+.

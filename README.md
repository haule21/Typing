# Typing v0.1

Typing is a Windows background utility designed for environments where direct copy-pasting is restricted (e.g., remote desktop sessions, VDI). It converts text from your clipboard into simulated keystrokes, effectively "typing" the content for you.

## Features

-   **Clipboard to Keystrokes**: Types out clipboard content character by character.
-   **Background Operation**: Runs silently in the system tray.
-   **Global Hotkey**: Trigger typing from anywhere (Default: `Ctrl + Shift + V`).
-   **Configurable**: Adjust typing speed and customize hotkeys.
-   **Cancellation**: Press **ESC** at any time to stop typing.
-   **Smart Modifiers**: Automatically handles modifier keys (Shift, Ctrl) during typing.

## Usage

1.  **Run** the application (`TypingApp.exe`). It will appear in the system tray.
2.  **Copy** text to your clipboard (`Ctrl + C`).
3.  **Place cursor** in the target field (e.g., inside a remote desktop window).
4.  **Press Hotkey** (`Ctrl + Shift + V` by default).
5.  The app will type the text.

**To Cancel**: Press **ESC** while typing is in progress.

## Architecture

The application is built with **.NET 9.0 (WPF)** and follows a modular architecture:

-   **Services**:
    -   `InputSimulator`: Uses Win32 `SendInput` API to simulate keystrokes. Handles special characters (`\n`, `\t`) and key delays.
    -   `HotkeyListener`: Implements low-level keyboard hooks (`SetWindowsHookEx`) to detect global hotkeys without focus.
    -   `ClipboardManager`: Safely retrieves clipboard text on the UI thread.
-   **Components**:
    -   `TrayContextManager`: Manages the system tray icon, context menu, and application lifecycle.
-   **Data**:
    -   `ConfigStore`: Persists user settings (Hotkey, Delay) to `config.json`.

## Configuration

Right-click the system tray icon and select **Settings** to:
-   Change the **Typing Delay** (milliseconds per character).
-   Set a custom **Hotkey** by clicking "Change" and pressing your desired combination.

## Installation

### Standalone EXE
[Download Latest Release](https://github.com/haule21/Typing/releases/latest/download/TypingApp.exe)
Single file executable. No installation required.
*Note: The application is self-signed (SHA256). You may see a SmartScreen warning.*

## Build

To build from source:
```bash
dotnet restore
dotnet build
```

To publish a single-file executable:
```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

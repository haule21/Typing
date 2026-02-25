# Typing v0.2

Typing is a Windows background utility designed for environments where direct copy-pasting is restricted (e.g., remote desktop sessions, VDI). It converts text from your clipboard into simulated keystrokes, effectively "typing" the content for you.

## Features

-   **Clipboard to Keystrokes**: Types out clipboard content character by character.
-   **Background Operation**: Runs silently in the system tray.
-   **Global Hotkey**: Trigger typing from anywhere (Default: `Ctrl + Shift + V`).
-   **Configurable**: Adjust typing speed (delay per character) and customize hotkeys.
-   **Execution Delay & Countdown**: Set a delay before typing starts, with a visual countdown overlay (0~10 seconds).
-   **Input Filtering**: Option to ignore Tab (`\t`) and Newline (`\n`) characters to prevent unintended formatting or submissions.
-   **Cancellation**: Press **ESC** at any time to stop typing.
-   **Smart Modifiers**: Automatically handles modifier keys (Shift, Ctrl) during typing.

## Usage

1.  **Install/Run** the application. It will appear in the system tray.
2.  **Copy** text to your clipboard (`Ctrl + C`).
3.  **Place cursor** in the target field (e.g., inside a remote desktop window).
4.  **Press Hotkey** (`Ctrl + Shift + V` by default).
5.  If an execution delay is set, a countdown will appear. Otherwise, typing starts immediately.

**To Cancel**: Press **ESC** while typing is in progress.

## Configuration

Right-click the system tray icon and select **Settings** to:
-   Change the **Typing Delay** (milliseconds per character).
-   Set a custom **Paste Hotkey** by clicking "Change" and pressing your desired combination.
-   Configure **Input Filtering**: Choose to exclude Tab (`\t`) and/or Newline (`\n`) characters.
-   Set an **Execution Delay** (0~10 seconds) before typing starts.

## Architecture

The application is built with **.NET 9.0 (WPF)** and follows a modular architecture:

-   **Services**:
    -   `InputSimulator`: Uses Win32 `SendInput` API to simulate keystrokes. Handles special characters, key delays, and input filtering.
    -   `HotkeyListener`: Implements low-level keyboard hooks (`SetWindowsHookEx`) to detect global hotkeys without focus.
    -   `ClipboardManager`: Safely retrieves clipboard text on the UI thread.
-   **Components**:
    -   `TrayContextManager`: Manages the system tray icon, context menu, and application lifecycle.
-   **Views**:
    -   `SettingsWindow`: UI for user configuration.
    -   `CountdownOverlay`: Transparent overlay showing the countdown timer before typing execution.
-   **Data**:
    -   `ConfigStore`: Persists user settings (Hotkey, Delays, Filtering) to `config.json`.

## Installation

### MSIX Package
The application is primarily distributed as an MSIX package for easy installation and clean uninstallation on Windows. You can download the latest `.msix` release from the repository's Releases page.

## Build

To build from source:
```bash
dotnet restore
dotnet build
```

To build an MSIX package (Release mode):
```bash
dotnet publish TypingApp.csproj -c Release -r win-x64
```
*(Note: MSIX packaging is enabled automatically in Release mode and requires a valid code signing certificate).*

For testing or debugging a standalone build without MSIX packaging, build in Debug mode:
```bash
dotnet build -c Debug
```

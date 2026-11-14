# NERO

NERO is a Windows-first, voice-oriented AI desktop assistant built for Windows 11.

## Current status — desktop shell and device Gemini setup

The current branch also adds a system-tray icon, a tray menu to reopen or exit NERO, close-to-tray behavior, current-user Windows sign-in startup registration, and start-minimized behavior. Windows CI has successfully restored, built, and tested the branch head at commit `47a0b850146496f353165e6b64bbaf7ad0060bc5`.

The desktop screen includes a basic prompt panel that sends a user-entered prompt to Gemini and displays the real provider response once a device key is configured.

## Current status — Phase 3 device Gemini setup

The application now has a device-local Gemini setup flow. A user can enter a key, validate it with Google's Gemini API, save it to Windows Credential Manager, replace it, or remove it. The secret is sent in the `x-goog-api-key` HTTP header and is not stored in `settings.json`.

The Windows microphone, wake word, speech recognition, tray integration, and automatic startup registration are not yet implemented.

## Current status — Phase 1 foundation

This branch establishes the first real application foundation:

- .NET 10 solution with a WPF desktop application.
- Dependency-injection host and application lifecycle wiring.
- A small platform-neutral core project.
- Core unit-test project.
- Windows GitHub Actions restore, build and test workflow.
- Shared build settings and secret-safe `.gitignore`.
- Initial architecture and security documentation.

The dashboard is intentionally honest: microphone capture and AI provider integration are not configured or running yet.

## Requirements

- .NET 10 SDK
- Windows 10/11 for running the WPF application
- Visual Studio 2022 with .NET desktop development workload, or the .NET CLI

## Build and test

```powershell
dotnet restore NERO.sln
dotnet build NERO.sln --configuration Release
dotnet test NERO.sln --configuration Release
```

## Roadmap

Development proceeds milestone by milestone: desktop lifecycle and tray behavior, device-local Gemini credential setup, real voice pipeline, AI orchestration and permission-gated tools, Windows/browser/document agents, local memory, licensing, signed updates, backend/admin services, and production installer.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Security

Never commit API keys, passwords, signing certificates, or local secrets. Gemini credentials will be device-local and stored through Windows Credential Manager when that feature is implemented. No microphone capture starts in the current foundation.

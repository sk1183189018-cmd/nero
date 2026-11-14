# NERO

NERO is a Windows-first AI desktop assistant for Windows 11, built with .NET 10 and WPF.

## Implemented in this development branch

- .NET 10 solution with WPF desktop application, dependency-injection host, and platform-neutral core.
- System-tray icon, close-to-tray behavior, tray reopen/exit actions, single-instance protection, Windows sign-in startup registration, and start-minimized preference.
- Local non-secret settings stored under the current user's LocalAppData folder.
- Gemini API key validation and storage in Windows Credential Manager. Keys are sent in the x-goog-api-key header and never written to settings.json or request URLs.
- Gemini text generation with a prompt/response panel.
- Push-to-talk recording at 16 kHz mono PCM. Audio remains in memory and is sent to Gemini only after Stop & send.
- Optional Hey NERO wake-word listening using the small English Vosk model. The user must explicitly enable it; wake-word detection is local and only the post-wake command clip is sent to Gemini.
- Gemini transcription guidance for Hindi, Hinglish, and English.
- Windows SAPI speech output using an installed voice that matches the selected language when available.
- Unit tests for settings persistence, Gemini request/response behavior, safe key handling, and Vosk model archive extraction.
- Windows GitHub Actions workflow for restore, build, and tests.

## Current limitations

- The Hey NERO wake-word model is English-only. Wake-word accuracy depends on microphone quality, room noise, and accent.
- The Vosk model is downloaded on first opt-in (about 40 MB) and uses roughly 300 MB of runtime memory according to its publisher.
- Hindi speech output requires a compatible Hindi SAPI voice installed in Windows.
- Voice requests currently receive answers; they do not yet execute Windows, browser, or Office actions.
- Hands-free "NERO Stop", advanced multilingual wake-word models, and speaker enrollment are not implemented.
- Production licensing, subscriptions, backend/admin dashboard, signed updater, rollback, and production installer remain future milestones.

## Requirements

- .NET 10 SDK
- Windows 11 for running the desktop app and its microphone/tray/credential features
- Visual Studio 2022 with the .NET desktop development workload, or the .NET CLI

## Build and test

```powershell
dotnet restore NERO.sln
dotnet build NERO.sln --configuration Release
dotnet test NERO.sln --configuration Release
```

## Security and privacy

- Never commit API keys, passwords, signing certificates, or local secrets.
- Gemini keys are device-local Windows Credential Manager secrets.
- Wake-word audio is processed locally. It is not sent to Gemini.
- After the user opts into Hey NERO, only the command recorded after wake-word detection is sent to Gemini.
- Push-to-talk audio is sent only after the user presses Stop & send.
- AI-generated responses do not execute computer actions in this branch.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/SECURITY.md](docs/SECURITY.md), and [docs/VOICE.md](docs/VOICE.md).

# NERO architecture

## Current milestone

The repository currently contains a .NET 10 WPF desktop application, a platform-neutral core assembly, Windows Credential Manager integration, a Gemini REST provider, local user preferences, a system-tray lifecycle, current-user startup registration, a test project, and Windows GitHub Actions CI. The desktop screen can validate/save/remove a per-device Gemini API key and send a text prompt to Gemini.

## Project boundaries

- `NERO.Desktop`: Windows Presentation Foundation application and composition root.
- `NERO.Core`: platform-neutral product primitives and contracts.
- `NERO.Core.Tests`: automated tests for core behavior.

## Planned boundaries

AI provider integrations, voice capture, Windows automation, browser automation, document processing, secure credential storage, local persistence, licensing, update services, and backend/admin services will be introduced as real implementations in dependency order. They are intentionally not represented by empty projects at this stage.

## Security principles

- No secrets are stored in source control.
- Microphone capture is not started by the initial shell.
- AI-proposed actions must pass through explicit validated tools and permission checks.
- Device-local Gemini credentials must use Windows Credential Manager when that milestone is implemented.
- Production update packages must be signed and verified before installation.

# NERO architecture

## Current milestone

The repository currently contains the initial .NET 10 solution foundation: a WPF desktop entry point, a small platform-neutral core assembly, a core test project, and a Windows GitHub Actions workflow.

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

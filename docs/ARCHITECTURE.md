# NERO architecture

## Current implementation

The solution currently contains these real projects:

- `NERO.Desktop`: .NET 10 WPF application, dependency-injection composition root, system tray, startup registration, Gemini settings, text chat, push-to-talk, wake-word controls, and speech output.
- `NERO.Core`: platform-neutral product metadata and persisted non-secret settings.
- `NERO.Security`: Windows Credential Manager integration for device-local secrets.
- `NERO.AI`: Gemini REST client, API-key validation, text generation, audio transcription, and structured action planning.
- `NERO.Automation`: allow-listed Windows application, website, search, known-folder, and folder-creation tools, all gated by explicit approval.
- `NERO.Voice`: WASAPI microphone capture, local Vosk wake-word detection, model download/validation, and Windows SAPI text-to-speech.
- `NERO.Core.Tests`: unit tests for core settings, Gemini behavior, and safe Vosk model extraction.

## Current data flow

1. Non-secret preferences are stored in the current user's LocalAppData directory.
2. The Gemini key is validated against Google's API and stored separately in Windows Credential Manager.
3. Text prompts are sent to Gemini over HTTPS.
4. Push-to-talk audio is buffered in memory and uploaded only after the user presses Stop & send.
5. If Hey NERO is enabled, Vosk processes microphone audio locally. After wake-word detection, a separate short command recording is captured and only that clip is sent to Gemini.
6. Gemini can also return a strict JSON action plan. NERO validates it against the allow-list and shows a confirmation dialog (default No) before executing any tool.
7. Gemini responses can be spoken with an installed Windows SAPI voice.

## Planned boundaries

The next milestones will expand Windows tools, add browser automation, Office/PDF document intelligence, local conversation memory, multi-step agent orchestration, account/device licensing, backend/admin services, signed updates with rollback, and a production installer. Full browser automation and Office/PDF/Excel intelligence are not implemented yet.

## Security principles

- No secrets are stored in source control or ordinary settings files.
- Gemini API keys remain in Windows Credential Manager.
- Wake-word audio is processed locally; command audio is uploaded only after explicit wake-word opt-in and detection.
- Push-to-talk audio is uploaded only after the user presses Stop & send.
- AI-proposed actions must pass through explicit, validated tools and permission checks before any future computer action.
- Production update packages must be signed and verified before installation.

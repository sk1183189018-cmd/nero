# NERO security notes

## Device-local Gemini API key

- The desktop setup flow validates a key against the configured Gemini model before storing it.
- The key is stored in Windows Credential Manager as a generic credential named NERO.Gemini.ApiKey.
- Credential persistence is local to the Windows device and current user profile.
- The key is never written to the ordinary JSON settings file, SQLite, source control, or application UI after save.
- Gemini requests send the key in the x-goog-api-key HTTP header, not the URL.
- The user can replace or remove the credential from the desktop settings screen.
- NERO account authentication, subscription licensing, and the user's Gemini API key are separate systems.

## Microphone and voice privacy

- Push-to-talk recording stays in memory and is uploaded only after the user presses Stop & send.
- Hey NERO listening is off by default and requires an explicit checkbox opt-in.
- The wake phrase is processed locally by Vosk. Wake-word audio is not sent to Gemini.
- After the wake phrase is detected, NERO records the following command locally and sends only that command clip to Gemini.
- Disabling wake-word listening or exiting NERO cancels the listener. Cancellation is checked before any command clip is uploaded.
- Windows SAPI speech output is local.

## AI action safety

- Gemini action planning uses structured JSON output, not shell commands.
- NERO accepts only the built-in allow-listed tools: open approved applications, open HTTP/HTTPS websites, web search, open known user folders, and create one folder under Desktop or Documents.
- Every action requires a separate user confirmation dialog. The default button is No.
- Tool arguments are validated again immediately before execution.
- There are no PowerShell, command prompt, arbitrary executable path, file deletion, registry-edit, purchase, or email-sending tools in this milestone.
- A model response is untrusted input. Unknown actions and invalid arguments are rejected without execution.

## Downloaded wake-word model

- The small English Vosk model is downloaded from the official Alpha Cephei HTTPS endpoint only after the user enables Hey NERO.
- The archive is checked for size limits and path traversal before extraction; the expected model files are checked before use.
- A vendor-published cryptographic checksum is not pinned in this milestone, so model download integrity beyond HTTPS and archive validation remains a hardening item.

## Current limitations

- NERO account authentication, subscription/device licensing, backend/admin services, signed updates/rollback, and the production installer are not implemented.
- Full browser automation and Office/PDF/Excel document intelligence are not implemented.
- Windows Credential Manager, microphone permissions, wake-word accuracy, and installed Hindi SAPI voices still need verification on a real target Windows PC.
- Never paste production keys into GitHub issues, commits, logs, screenshots, or support reports.

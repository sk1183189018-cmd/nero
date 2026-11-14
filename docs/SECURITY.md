# NERO security notes

## Device-local Gemini API key

- The desktop setup flow validates a key with the Gemini API before storing it.
- The key is stored in Windows Credential Manager as a generic credential named `NERO.Gemini.ApiKey`.
- Credential persistence is local to the Windows device and current user profile.
- The key is never written to the ordinary JSON settings file, SQLite, source control, or application UI after save.
- Gemini requests send the key in the `x-goog-api-key` HTTP header, not the URL.
- The user can replace or remove the credential from the desktop settings screen.
- NERO account authentication, subscription licensing, and the user's Gemini API key are separate systems.

## Current limitations

- This milestone does not implement a NERO account or licensing backend.
- This milestone does not implement voice capture or wake-word monitoring.
- Windows Credential Manager behavior must be verified on a real Windows user profile; CI compiles and tests the provider logic but does not validate a real user's credential vault.
- Never paste production keys into GitHub issues, commits, logs, screenshots, or support reports.

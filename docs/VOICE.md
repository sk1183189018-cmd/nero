# NERO voice system

## Current implementation

The desktop app provides an explicit push-to-talk recording flow:

1. The user clicks Start recording.
2. NERO captures microphone audio locally into an in-memory WAV buffer.
3. The user clicks Stop & send.
4. Only then does NERO send that single recording to Gemini for transcription.
5. NERO sends the resulting transcript to Gemini for a response.
6. NERO speaks the answer using an installed Windows SAPI voice when available. The user can stop speech at any time.

The recorder does not create audio files and does not upload or stream audio by itself. A recording is capped at 12 MiB. The user can stop it at any time.

## Audio dependencies

- NAudio.Wasapi is used for Windows WASAPI capture. Its project license is MIT.
- Gemini receives a single audio/wav inline payload only after the user explicitly selects Stop & send.

## Not implemented yet

- Background wake-word listening.
- Custom Hey NERO model.
- Automatic speech recognition while running in the tray.
- Guaranteed Hindi text-to-speech on machines without an installed Hindi SAPI voice.
- Voice cancellation phrase (NERO Stop).

The custom wake word is intentionally not enabled. Picovoice's .NET SDK supports Windows, but a custom keyword model requires Picovoice Console assets and commercial licensing for a commercial product. Those credentials and model files must be supplied through the product's secure setup/release process, not committed to source control.

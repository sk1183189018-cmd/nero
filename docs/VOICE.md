# NERO voice system

## Push-to-talk voice input

1. The user clicks Start recording.
2. NERO captures 16 kHz mono PCM microphone audio locally into an in-memory WAV buffer.
3. The user clicks Stop & send.
4. Only then does NERO send that single recording to Gemini for transcription.
5. NERO sends the resulting transcript to Gemini for a response.
6. NERO speaks the answer using an installed Windows SAPI voice when available. The user can stop speech at any time.

The recorder does not create audio files and does not upload or stream audio by itself. A recording is capped at 12 MiB. The user can stop it at any time.

## Hey NERO local wake word

- The user must explicitly enable the Hey NERO checkbox. It is off by default.
- On first enable, NERO downloads the official small English Vosk model (about 40 MB) from Alpha Cephei over HTTPS and validates the archive paths and model files before use.
- Vosk listens locally for the exact English phrase "Hey NERO" using a constrained grammar. It does not send the wake-word audio to Gemini.
- After detection, NERO captures the following command locally and ends the clip after speech silence. Only that command clip is sent to Gemini for Hindi, Hinglish, or English transcription.
- Say "Hey NERO", pause briefly for the detection prompt, and then speak the command.
- Disable the checkbox or exit NERO to stop continuous local wake-word listening.

Wake-word accuracy depends on the microphone, room noise, and accent. The current model is English-only; the command itself can be Hindi, Hinglish, or English because Gemini processes the post-wake audio.

## Speech output

NERO uses the installed Windows SAPI voice through System.Speech. Speech can be interrupted. Hindi output is available only if a compatible Hindi voice is installed in Windows; otherwise the user can still read the text response.

## Dependencies and licenses

- NAudio.Wasapi: MIT; Windows WASAPI capture.
- System.Speech: MIT; Windows SAPI text-to-speech.
- Vosk: Apache-2.0; local speech recognition.
- Vosk small English model: Apache-2.0; downloaded on first opt-in.

## Not implemented yet

- Custom speaker enrollment and multilingual wake-word models.
- Hands-free interruption phrase ("NERO Stop") while NERO is speaking.
- Voice commands that execute Windows/browser actions. The current voice path transcribes and answers but does not claim to perform computer actions.

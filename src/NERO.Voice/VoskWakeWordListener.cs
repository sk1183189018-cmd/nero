using System.Text.Json;
using NAudio.Wave;
using Vosk;

namespace NERO.Voice;

/// <summary>Runs the Hey NERO phrase grammar locally; microphone audio is never sent to this service.</summary>
public sealed class VoskWakeWordListener : IWakeWordListener
{
    private const float SampleRate = 16000.0f;
    private const string WakeGrammar = "[\"hey nero\"]";
    private readonly IWakeWordModelManager _modelManager;

    public VoskWakeWordListener(IWakeWordModelManager modelManager)
    {
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
    }

    public Task PrepareAsync(CancellationToken cancellationToken = default) =>
        _modelManager.EnsureModelAsync(cancellationToken);

    public async Task ListenAsync(
        Func<CancellationToken, Task> onWakeWord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onWakeWord);
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The NERO wake-word listener requires Windows WASAPI.");

        var modelPath = await _modelManager.EnsureModelAsync(cancellationToken).ConfigureAwait(false);
        Vosk.Vosk.SetLogLevel(-1);
        using var model = new Model(modelPath);

        while (!cancellationToken.IsCancellationRequested)
        {
            var detected = false;
            using var recognizer = new VoskRecognizer(model, SampleRate, WakeGrammar);
            await using (var recorder = new WasapiRecorderBuilder()
                .WithFormat(new WaveFormat((int)SampleRate, 16, 1))
                .Build())
            {
                await foreach (var audio in recorder.CaptureAsync(cancellationToken).ConfigureAwait(false))
                {
                    var bytes = audio.Data.ToArray();
                    if (!recognizer.AcceptWaveform(bytes, bytes.Length)) continue;

                    using var result = JsonDocument.Parse(recognizer.Result());
                    var phrase = result.RootElement.TryGetProperty("text", out var text)
                        ? text.GetString()
                        : null;
                    if (string.Equals(phrase?.Trim(), "hey nero", StringComparison.OrdinalIgnoreCase))
                    {
                        detected = true;
                        break;
                    }
                }
            }

            if (detected)
                await onWakeWord(cancellationToken).ConfigureAwait(false);
        }
    }
}

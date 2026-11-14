using System.Runtime.Versioning;
using System.Speech.Synthesis;

namespace NERO.Voice;

/// <summary>Uses Windows SAPI voices installed for the current user.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsTextToSpeech : ITextToSpeech
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public async Task SpeakAsync(string text, string cultureName = "auto", CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows speech synthesis is available only on Windows.");
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(cultureName) && cultureName != "auto")
            {
                var preferredVoice = _synthesizer.GetInstalledVoices()
                    .FirstOrDefault(voice => voice.Enabled &&
                        string.Equals(voice.VoiceInfo.Culture.Name, cultureName, StringComparison.OrdinalIgnoreCase));
                if (preferredVoice is not null) _synthesizer.SelectVoice(preferredVoice.VoiceInfo.Name);
            }
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<SpeakCompletedEventArgs>? handler = null;
            handler = (_, args) =>
            {
                _synthesizer.SpeakCompleted -= handler;
                if (args.Error is not null) completion.TrySetException(args.Error);
                else if (args.Cancelled) completion.TrySetCanceled(cancellationToken);
                else completion.TrySetResult();
            };

            _synthesizer.SpeakCompleted += handler;
            using var registration = cancellationToken.Register(Stop);
            try
            {
                _synthesizer.SpeakAsync(text);
                await completion.Task.ConfigureAwait(false);
            }
            catch
            {
                _synthesizer.SpeakCompleted -= handler;
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Stop()
    {
        if (!_disposed) _synthesizer.SpeakAsyncCancelAll();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _synthesizer.SpeakAsyncCancelAll();
        _synthesizer.Dispose();
    }
}

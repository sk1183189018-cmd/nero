using System.Runtime.Versioning;
using NAudio.Wave;

namespace NERO.Voice;

/// <summary>
/// Captures from the default Windows microphone into an in-memory WAV buffer.
/// Audio is not uploaded or written to disk by this class.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsMicrophoneRecorder : IMicrophoneRecorder
{
    private const long MaximumRecordingBytes = 12 * 1024 * 1024;

    public async Task<byte[]> RecordWaveUntilCancelledAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Microphone recording is supported only on Windows.");
        cancellationToken.ThrowIfCancellationRequested();

        await using var recorder = new WasapiRecorderBuilder().Build();
        using var output = new MemoryStream();

        using (var writer = new WaveFileWriter(output, recorder.WaveFormat))
        {
            try
            {
                await foreach (var buffer in recorder.CaptureAsync(cancellationToken).ConfigureAwait(false))
                {
                    writer.Write(buffer.Data.Span);
                    if (output.Length > MaximumRecordingBytes)
                        throw new InvalidOperationException("The recording reached the size limit. Stop and send a shorter voice command.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // User requested Stop; return the audio captured so far.
            }
        }

        return output.ToArray();
    }
}

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

        await using var recorder = new WasapiRecorderBuilder()
            .WithFormat(new WaveFormat(16000, 16, 1))
            .Build();
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

    public async Task<byte[]> RecordCommandUntilSilenceAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Microphone recording is supported only on Windows.");
        cancellationToken.ThrowIfCancellationRequested();

        await using var recorder = new WasapiRecorderBuilder()
            .WithFormat(new WaveFormat(16000, 16, 1))
            .Build();
        using var output = new MemoryStream();
        var speechStarted = false;
        var silenceMilliseconds = 0.0;
        var elapsedMilliseconds = 0.0;

        using (var writer = new WaveFileWriter(output, recorder.WaveFormat))
        {
            try
            {
                await foreach (var buffer in recorder.CaptureAsync(cancellationToken).ConfigureAwait(false))
                {
                    var data = buffer.Data.ToArray();
                    writer.Write(data);
                    var durationMilliseconds = data.Length * 1000.0 / (16000 * 2);
                    elapsedMilliseconds += durationMilliseconds;
                    var rms = CalculateRms(data);

                    if (rms >= 550)
                    {
                        speechStarted = true;
                        silenceMilliseconds = 0;
                    }
                    else if (speechStarted)
                    {
                        silenceMilliseconds += durationMilliseconds;
                    }

                    if ((speechStarted && silenceMilliseconds >= 1200) || elapsedMilliseconds >= 30000)
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The user disabled wake-word listening or exited NERO.
            }
        }

        return speechStarted ? output.ToArray() : Array.Empty<byte>();
    }

    private static double CalculateRms(byte[] pcm16)
    {
        if (pcm16.Length < 2) return 0;
        double sum = 0;
        var samples = pcm16.Length / 2;
        for (var index = 0; index + 1 < pcm16.Length; index += 2)
        {
            var sample = (short)(pcm16[index] | (pcm16[index + 1] << 8));
            sum += sample * (double)sample;
        }
        return Math.Sqrt(sum / samples);
    }

}

using System.IO.Compression;

namespace NERO.Voice;

/// <summary>Downloads the official small English Vosk model on first opt-in.</summary>
public sealed class VoskWakeWordModelManager : IWakeWordModelManager
{
    private const string ModelName = "vosk-model-small-en-us-0.15";
    private static readonly Uri ModelArchive = new($"https://alphacephei.com/vosk/models/{ModelName}.zip");
    private const long MaximumArchiveBytes = 100 * 1024 * 1024;
    private const long MaximumExpandedBytes = 250 * 1024 * 1024;
    private readonly HttpClient _httpClient;
    private readonly string _modelsDirectory;

    public VoskWakeWordModelManager(HttpClient httpClient, string? modelsDirectory = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _modelsDirectory = modelsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NERO", "Models");
    }

    public async Task<string> EnsureModelAsync(CancellationToken cancellationToken = default)
    {
        var destination = Path.Combine(_modelsDirectory, ModelName);
        if (IsValidModel(destination)) return destination;

        Directory.CreateDirectory(_modelsDirectory);
        var archivePath = Path.Combine(_modelsDirectory, ModelName + ".zip");
        var extractionPath = Path.Combine(_modelsDirectory, ModelName + ".extract-" + Guid.NewGuid().ToString("N"));

        try
        {
            using var response = await _httpClient.GetAsync(
                ModelArchive, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > MaximumArchiveBytes)
                throw new InvalidDataException("The wake-word model archive exceeds the download size limit.");

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var target = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                if (target.Length > MaximumArchiveBytes)
                    throw new InvalidDataException("The wake-word model archive exceeds the download size limit.");
            }

            Directory.CreateDirectory(extractionPath);
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                long expandedBytes = 0;
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    expandedBytes = checked(expandedBytes + entry.Length);
                    if (expandedBytes > MaximumExpandedBytes)
                        throw new InvalidDataException("The extracted wake-word model exceeds the size limit.");

                    var relativePath = entry.FullName.Replace('\\', '/');
                    if (relativePath.StartsWith('/') || relativePath.Split('/').Any(part => part == ".."))
                        throw new InvalidDataException("The wake-word archive contains an unsafe path.");

                    var targetPath = Path.GetFullPath(Path.Combine(extractionPath, relativePath));
                    var rootPath = Path.GetFullPath(extractionPath) + Path.DirectorySeparatorChar;
                    if (!targetPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The wake-word archive contains an unsafe path.");

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(targetPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    await using var entryStream = entry.Open();
                    await using var output = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                    await entryStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                }
            }

            var extractedModel = Path.Combine(extractionPath, ModelName);
            if (!Directory.Exists(extractedModel)) extractedModel = extractionPath;
            if (!IsValidModel(extractedModel))
                throw new InvalidDataException("The downloaded archive did not contain a valid Vosk English model.");

            if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
            Directory.Move(extractedModel, destination);
            return destination;
        }
        finally
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);
            if (Directory.Exists(extractionPath)) Directory.Delete(extractionPath, recursive: true);
        }
    }

    private static bool IsValidModel(string path) =>
        Directory.Exists(path) &&
        File.Exists(Path.Combine(path, "am", "final.mdl")) &&
        File.Exists(Path.Combine(path, "conf", "mfcc.conf")) &&
        File.Exists(Path.Combine(path, "graph", "HCLG.fst"));
}

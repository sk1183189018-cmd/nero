using System.IO.Compression;
using NERO.Voice;
using Xunit;

namespace NERO.Core.Tests;

public sealed class VoskWakeWordModelManagerTests
{
    [Fact]
    public async Task EnsureModelAsync_DownloadsAndCachesValidatedModel()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var handler = new StubHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(CreateArchive(includeTraversalEntry: false))
        });
        using var http = new HttpClient(handler);

        try
        {
            var manager = new VoskWakeWordModelManager(http, root);
            var path = await manager.EnsureModelAsync();
            var cachedPath = await manager.EnsureModelAsync();

            Assert.Equal(path, cachedPath);
            Assert.Equal(1, handler.RequestCount);
            Assert.True(File.Exists(Path.Combine(path, "am", "final.mdl")));
            Assert.True(File.Exists(Path.Combine(path, "conf", "mfcc.conf")));
            Assert.True(File.Exists(Path.Combine(path, "graph", "HCLG.fst")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureModelAsync_RejectsZipSlipEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var handler = new StubHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(CreateArchive(includeTraversalEntry: true))
        });
        using var http = new HttpClient(handler);

        try
        {
            var manager = new VoskWakeWordModelManager(http, root);

            await Assert.ThrowsAsync<InvalidDataException>(() => manager.EnsureModelAsync());

            Assert.False(File.Exists(Path.Combine(root, "evil.txt")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static byte[] CreateArchive(bool includeTraversalEntry)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "vosk-model-small-en-us-0.15/am/final.mdl");
            AddEntry(archive, "vosk-model-small-en-us-0.15/conf/mfcc.conf");
            AddEntry(archive, "vosk-model-small-en-us-0.15/graph/HCLG.fst");
            if (includeTraversalEntry) AddEntry(archive, "../evil.txt");
        }
        return stream.ToArray();
    }

    private static void AddEntry(ZipArchive archive, string path)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write("test");
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responseFactory(request));
        }
    }
}

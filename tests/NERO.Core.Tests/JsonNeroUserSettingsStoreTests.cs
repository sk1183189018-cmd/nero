using NERO.Core.Settings;
using Xunit;

namespace NERO.Core.Tests;

public sealed class JsonNeroUserSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_WhenMissing_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
        var settings = await new JsonNeroUserSettingsStore(path).LoadAsync();
        Assert.False(settings.LaunchAtStartup);
        Assert.False(settings.StartMinimized);
        Assert.False(settings.MicrophoneListeningEnabled);
        Assert.False(settings.WakeWordEnabled);
        Assert.Equal("auto", settings.SpeechLanguage);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesPreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        var store = new JsonNeroUserSettingsStore(path);
        var expected = new NeroUserSettings { LaunchAtStartup = true, StartMinimized = true, SpeechLanguage = "hi-IN" };
        await store.SaveAsync(expected);
        Assert.Equal(expected, await store.LoadAsync());
        Directory.Delete(directory, recursive: true);
    }
}

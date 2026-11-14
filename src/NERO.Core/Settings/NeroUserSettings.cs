namespace NERO.Core.Settings;

/// <summary>Non-secret preferences stored in the current Windows user profile.</summary>
public sealed record NeroUserSettings
{
    public bool LaunchAtStartup { get; init; }
    public bool StartMinimized { get; init; }
    public bool WakeWordEnabled { get; init; }
    public string SpeechLanguage { get; init; } = "auto";
}

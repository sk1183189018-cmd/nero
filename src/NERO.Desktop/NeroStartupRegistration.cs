using Microsoft.Win32;

namespace NERO.Desktop;

/// <summary>Registers NERO in the current user's Windows sign-in startup list.</summary>
internal static class NeroStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NERO";

    public static void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows startup registration is available only on Windows.");

        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Windows could not open the current-user startup registry key.");

        if (!enabled)
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Windows did not provide the NERO executable path.");
        runKey.SetValue(ValueName, string.Concat("\"", executable, "\" --startup"), RegistryValueKind.String);
    }
}

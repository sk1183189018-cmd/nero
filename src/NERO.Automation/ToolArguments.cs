using System.Text.Json;

namespace NERO.Automation;

internal static class ToolArguments
{
    public static string RequiredString(JsonElement arguments, string name, int maximumLength = 512)
    {
        if (arguments.ValueKind != JsonValueKind.Object ||
            !arguments.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String)
            throw new ArgumentException($"The action is missing the '{name}' value.");

        var result = value.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(result))
            throw new ArgumentException($"The '{name}' value cannot be empty.");
        if (result.Length > maximumLength)
            throw new ArgumentException($"The '{name}' value is too long.");
        return result;
    }
}

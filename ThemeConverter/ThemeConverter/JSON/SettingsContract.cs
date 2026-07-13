
using System.Text.Json.Serialization;

namespace ThemeConverter.JSON;

internal sealed record SettingsContract
{
    [JsonPropertyName("foreground")]
    public string? Foreground { get; set; }

    [JsonPropertyName("background")]
    public string? Background { get; set; }
}

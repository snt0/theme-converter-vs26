using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ThemeConverter.JSON;

internal sealed record ThemeFileContract
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("colors")]
    public Dictionary<string, string?>? Colors { get; set; }

    [JsonIgnore]
    public Dictionary<string, string?> ColorValues
    {
        get => Colors ??= [];
    }

    [JsonPropertyName("tokenColors")]
    public RuleContract?[]? TokenColors { get; set; }

    [JsonIgnore]
    public IEnumerable<RuleContract> TokenColorRules
    {
        get => TokenColors?.OfType<RuleContract>() ?? [];
    }
}
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThemeConverter.JSON;

internal sealed record RuleContract
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("scope")]
    public JsonElement Scope { get; set; }

    [JsonPropertyName("settings")]
    public SettingsContract? Settings { get; set; }

    [JsonIgnore]
    public SettingsContract ColorSettings
    {
        get => Settings ??= new SettingsContract();
    }

    public string[] ScopeNames
    {
        get
        {
            return Scope.ValueKind switch
                   {
                       JsonValueKind.String => Scope.GetString() is { } scope ? [scope] : [],
                       JsonValueKind.Array => [..(Scope.Deserialize<string?[]>() ?? []).OfType<string>()],
                       JsonValueKind.Undefined or JsonValueKind.Null => [],
                       _ => throw new JsonException("The scope property must be a string or an array of strings.")
                   };
        }
    }
}
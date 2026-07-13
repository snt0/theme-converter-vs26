// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThemeConverter;

internal static class ParseMapping
{
    private const string TokenColorsName = "tokenColors";
    private const string VscTokenName = "VSC Token";
    private const string VsTokenName = "VS Token";
    private const string TokenMappingFileName = "TokenMappings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip, WriteIndented = true
    };

    public static void CheckDuplicateMapping(Action<string> reportFunc)
    {
        TokenMappingFile file = DeserializeRequired<TokenMappingFile>(TokenMappingFileName);

        HashSet<string> mappedVsTokens = new(StringComparer.Ordinal);

        foreach (TokenMapping color in file.TokenColors)
        {
            foreach (string vsToken in color.VsTokens.Where(vsToken => !mappedVsTokens.Add(vsToken)))
            {
                reportFunc(color.VscToken + ": " + vsToken);
            }
        }
    }

    public static Dictionary<string, ColorKey[]> CreateScopeMapping()
    {
        TokenMappingFile file = DeserializeRequired<TokenMappingFile>(TokenMappingFileName);
        Dictionary<string, ColorKey[]> scopeMappings = new(StringComparer.Ordinal);
        foreach (TokenMapping color in file.TokenColors)
        {
            List<ColorKey> values = [];
            foreach (ColorKey newColorKey in color.VsTokens.Select(vsToken => vsToken.Split("&")).Select(colorKey =>
                         colorKey.Length switch
                         {
                             2 => // category & token name (by default foreground)
                                 new ColorKey(colorKey[0], colorKey[1], "Foreground"),
                             3 => // category & token name & aspect
                                 new ColorKey(colorKey[0], colorKey[1], colorKey[2]),
                             4 => // category & token name & vsc opacity & vscode background
                                 new ColorKey(colorKey[0], colorKey[1], "Foreground", colorKey[2], colorKey[3]),
                             5 => // category & token name & aspect & vsc opacity & vscode background
                                 new ColorKey(colorKey[0], colorKey[1], colorKey[2], colorKey[3], colorKey[4]),
                             _ => throw new InvalidDataException(
                                 "A token mapping must contain between two and five '&'-delimited fields.")
                         }))
            {
                values.Add(newColorKey);
            }

            scopeMappings.Add(color.VscToken, [.. values]);
        }

        return scopeMappings;
    }

    public static Dictionary<string, string> CreateCategoryGuids()
    {
        return DeserializeRequired<Dictionary<string, string>>("CategoryGuid.json");
    }

    public static Dictionary<string, string> CreateVscTokenFallback()
    {
        return DeserializeRequired<Dictionary<string, string>>("VSCTokenFallback.json");
    }

    public static Dictionary<string, (float, string)> CreateOverlayMapping()
    {
        Dictionary<string, OverlayMappingValue> values =
            DeserializeRequired<Dictionary<string, OverlayMappingValue>>("OverlayMapping.json");
        return values.ToDictionary(item => item.Key, item => (item.Value.Opacity, item.Value.Background),
            StringComparer.Ordinal);
    }

    private static T DeserializeRequired<T>(string fileName)
    {
        string contents = File.ReadAllText(GetDataFilePath(fileName));
        return JsonSerializer.Deserialize<T>(contents, JsonOptions) ??
               throw new JsonException($"{fileName} does not contain the expected JSON structure.");
    }

    internal static string GetDataFilePath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Data", fileName);
    }

    private sealed record TokenMappingFile
    {
        [JsonPropertyName(TokenColorsName)]
        public TokenMapping[] TokenColors { get; init; } = [];
    }

    private sealed record TokenMapping
    {
        [JsonPropertyName(VscTokenName)]
        public required string VscToken { get; init; }

        [JsonPropertyName(VsTokenName)]
        public string[] VsTokens { get; init; } = [];
    }

    private sealed record OverlayMappingValue
    {
        [JsonPropertyName("Item1")]
        public float Opacity { get; init; }

        [JsonPropertyName("Item2")]
        public required string Background { get; init; }
    }
}
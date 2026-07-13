// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ThemeConverter.JSON;

internal static class VsCodeThemeReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static async Task<ThemeFileContract> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        string json = await File.ReadAllTextAsync(path, cancellationToken);

        try
        {
            return Parse(json);
        }
        catch (JsonException exception)
        {
            throw new JsonException($"Failed to parse VS Code theme '{path}': {exception.Message}", exception);
        }
    }

    internal static ThemeFileContract Parse(string json)
    {
        string normalizedJson = NormalizeGeneratedTheme(json);
        return JsonSerializer.Deserialize<ThemeFileContract>(normalizedJson, JsonOptions) ??
               throw new JsonException("The theme file does not contain a JSON object.");
    }

    private static string NormalizeGeneratedTheme(string json)
    {
        string[] lines = json.ReplaceLineEndings("\n").Split('\n');
        List<int> restoredProperties = [];

        for (int index = 0; index < lines.Length; index++)
        {
            ReadOnlySpan<char> trimmed = lines[index].AsSpan().TrimStart();
            if (!trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            ReadOnlySpan<char> comment = trimmed[2..].TrimStart();
            if (!IsJsonProperty(comment))
            {
                continue;
            }

            int commentMarker = lines[index].IndexOf("//", StringComparison.Ordinal);
            lines[index] = lines[index].Remove(commentMarker, 2);
            restoredProperties.Add(index);
        }

        foreach (int restoredProperty in restoredProperties)
        {
            int previousLine = FindPreviousContentLine(lines, restoredProperty);
            if (previousLine >= 0 && NeedsSeparator(lines[previousLine]))
            {
                lines[previousLine] = lines[previousLine].TrimEnd() + ',';
            }
        }

        return string.Join('\n', lines);
    }

    private static bool IsJsonProperty(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || text[0] != '"')
        {
            return false;
        }

        bool escaped = false;
        for (int index = 1; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '"' && !escaped)
            {
                return text[(index + 1)..].TrimStart().StartsWith(":", StringComparison.Ordinal);
            }

            escaped = character == '\\' && !escaped;
            if (character != '\\')
            {
                escaped = false;
            }
        }

        return false;
    }

    private static int FindPreviousContentLine(IReadOnlyList<string> lines, int startIndex)
    {
        for (int index = startIndex - 1; index >= 0; index--)
        {
            string candidate = lines[index].Trim();
            if (candidate.Length > 0 && !candidate.StartsWith("//", StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool NeedsSeparator(string line)
    {
        ReadOnlySpan<char> content = line.AsSpan().TrimEnd();
        return !content.IsEmpty && content[^1] is not ('{' or '[' or ',');
    }
}
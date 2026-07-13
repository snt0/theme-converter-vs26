// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using ThemeConverter;
using Xunit;

namespace ThemeConverterTests;

public class VsCodeThemeReaderTest
{
    [Fact]
    public async Task Convert_StandardTheme()
    {
        string[] pkgdef = await ConvertAsync("""
            {
              "type": "dark",
              "colors": { "editor.background": "#112233" },
              "tokenColors": [
                { "scope": "keyword", "settings": { "foreground": "#abcdef" } }
              ]
            }
            """);

        pkgdef[3].Should().Be("\"FallbackId\"=\"{1ded0138-47ce-435e-84ef-9ec1f439b749}\"");
        pkgdef.Count(line => line.StartsWith("\"Data\"=hex:", StringComparison.Ordinal)).Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task Convert_JsoncCommentsAndTrailingCommas()
    {
        string[] pkgdef = await ConvertAsync("""
            // A generated VS Code color theme.
            {
              "type": "light", // Inline comments remain comments.
              "colors": {
                "editor.background": "#ffffff",
              },
            }
            """);

        pkgdef[3].Should().Be("\"FallbackId\"=\"{de3dbbcd-f642-433c-8353-8f1df4370aba}\"");
    }

    [Fact]
    public async Task Convert_RestoresCommentedPropertiesAndMissingSeparators()
    {
        string[] pkgdef = await ConvertAsync("""
            {
              "type": "dark",
              "colors": {
                "editor.background": "#000000"
                // "editor.foreground": "#ffffff"
                // This prose comment must not become JSON.
                // "editor.selectionBackground": "#123456",
              }
            }
            """);

        pkgdef.Count(line => line.StartsWith("\"Data\"=hex:", StringComparison.Ordinal)).Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task Convert_CommentOnFirstLineDoesNotReadBeforeTheInput()
    {
        string[] pkgdef = await ConvertAsync("""
            // Header comment
            { "colors": {} }
            """);

        pkgdef.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("#abc")]
    [InlineData("#abcd")]
    [InlineData("#aabbcc")]
    [InlineData("#aabbccdd")]
    public async Task Convert_SupportsAllVsCodeHexColorForms(string color)
    {
        string[] pkgdef = await ConvertAsync($$"""
            { "colors": { "editor.background": "{{color}}" } }
            """);

        pkgdef.Should().Contain(line => line.StartsWith("\"Data\"=hex:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Convert_RejectsUnsupportedHexColorForms()
    {
        Func<Task> convert = async () => await ConvertAsync("{ \"colors\": { \"editor.background\": \"#12\" } }");

        (await convert.Should().ThrowAsync<FormatException>()).Which.Message.Should().Contain("RGB, RGBA, RRGGBB, or RRGGBBAA");
    }

    [Fact]
    public async Task Convert_MalformedThemeIncludesTheSourcePathInTheError()
    {
        string sourcePath = GetSourcePath();
        try
        {
            await File.WriteAllTextAsync(sourcePath, "{ \"colors\": [ }");

            Func<Task> convert = () => Converter.ConvertFileAsync(sourcePath, Path.GetDirectoryName(sourcePath)!);

            (await convert.Should().ThrowAsync<JsonException>()).Which.Message.Should().Contain(sourcePath);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(sourcePath)!, true);
        }
    }

    private static async Task<string[]> ConvertAsync(string json)
    {
        string sourcePath = GetSourcePath();
        try
        {
            await File.WriteAllTextAsync(sourcePath, json);
            string outputPath = await Converter.ConvertFileAsync(sourcePath, Path.GetDirectoryName(sourcePath)!);
            return await File.ReadAllLinesAsync(outputPath);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(sourcePath)!, true);
        }
    }

    private static string GetSourcePath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ThemeConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "TestTheme.json");
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using ThemeConverter;
using Xunit;

namespace ThemeConverterTests;

public class DeserializationNullabilityTest
{
    [Fact]
    public async Task Convert_NormalizesExplicitNullOptionalThemeValues()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ThemeConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string sourcePath = Path.Combine(directory, "NullValues.json");
            await File.WriteAllTextAsync(sourcePath, """
                {
                  "colors": null,
                  "tokenColors": [null, { "scope": ["keyword", null], "settings": null }]
                }
                """);

            string outputPath = await Converter.ConvertFileAsync(sourcePath, directory);

            File.Exists(outputPath).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}

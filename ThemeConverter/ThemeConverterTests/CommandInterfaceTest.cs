// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Spectre.Console.Cli.Testing;
using ThemeConverter;
using Xunit;

namespace ThemeConverterTests;

public class CommandInterfaceTest
{
    [Fact]
    public void RootHelp_ListsSeparateWorkflows()
    {
        CommandAppResult result = Run("--help");

        result.ExitCode.Should().Be(0);
        result.Output.Should().Contain("convert <INPUT>");
        result.Output.Should().Contain("install <PKGDEF>");
    }

    [Fact]
    public async Task Convert_WritesPkgDefWithoutVisualStudioOptions()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string inputPath = Path.Combine(directory, "Sample.jsonc");
            string outputPath = Path.Combine(directory, "output");
            await File.WriteAllTextAsync(inputPath, "{ \"type\": \"dark\", \"colors\": {} }");

            CommandAppResult result = Run("convert", inputPath, "--output", outputPath);

            result.ExitCode.Should().Be(0, result.Output);
            File.Exists(Path.Combine(outputPath, "Sample.pkgdef")).Should().BeTrue();
            result.Output.Should().Contain("Created");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Install_RefusesExistingThemeWithoutForceBeforeStartingVisualStudio()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string inputPath = Path.Combine(directory, "Sample.pkgdef");
            await File.WriteAllTextAsync(inputPath, "theme");

            string visualStudioRoot = Path.Combine(directory, "VisualStudio");
            string ideDirectory = Path.Combine(visualStudioRoot, "Common7", "IDE");
            string themeDirectory = Path.Combine(ideDirectory, "CommonExtensions", "Platform");
            Directory.CreateDirectory(themeDirectory);
            await File.WriteAllTextAsync(Path.Combine(ideDirectory, "devenv.exe"), string.Empty);
            await File.WriteAllTextAsync(Path.Combine(themeDirectory, "Sample.pkgdef"), "existing");

            CommandAppResult result = Run("install", inputPath, "--target-vs", visualStudioRoot);

            result.ExitCode.Should().NotBe(0);
            result.Output.Should().Contain("--force");
            (await File.ReadAllTextAsync(Path.Combine(themeDirectory, "Sample.pkgdef"))).Should().Be("existing");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static CommandAppResult Run(params string[] arguments)
    {
        CommandAppTester app = new();
        app.Configure(Program.Configure);
        return app.Run(arguments);
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ThemeConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
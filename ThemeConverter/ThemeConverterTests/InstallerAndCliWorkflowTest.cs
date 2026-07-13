// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using ThemeConverter;
using Xunit;

namespace ThemeConverterTests;

public class InstallerAndCliWorkflowTest
{
    [Fact]
    public async Task ConvertCommand_ConvertsEveryThemeFileInDirectory()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string inputDirectory = Path.Combine(directory, "input");
            string outputDirectory = Path.Combine(directory, "output");
            Directory.CreateDirectory(inputDirectory);
            await File.WriteAllTextAsync(Path.Combine(inputDirectory, "Dark.json"), "{ \"type\": \"dark\", \"colors\": {} }");
            await File.WriteAllTextAsync(Path.Combine(inputDirectory, "Light.jsonc"), "{ \"type\": \"light\", \"colors\": {} }");
            await File.WriteAllTextAsync(Path.Combine(inputDirectory, "ignored.txt"), "not a theme");

            int exitCode = await RunProgramAsync("convert", inputDirectory, "--output", outputDirectory);

            exitCode.Should().Be(0);
            File.Exists(Path.Combine(outputDirectory, "Dark.pkgdef")).Should().BeTrue();
            File.Exists(Path.Combine(outputDirectory, "Light.pkgdef")).Should().BeTrue();
            File.Exists(Path.Combine(outputDirectory, "ignored.pkgdef")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task InstallCommand_OverwritesAndUpdatesConfiguration()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string inputPath = Path.Combine(directory, "Sample.pkgdef");
            await File.WriteAllTextAsync(inputPath, "new theme");
            string visualStudioRoot = CreateVisualStudioLayout(directory);
            string installedPath = Path.Combine(visualStudioRoot, "Common7", "IDE", "CommonExtensions", "Platform", "Sample.pkgdef");
            await File.WriteAllTextAsync(installedPath, "old theme");

            int exitCode = await RunProgramAsync("install", inputPath, "--target-vs", visualStudioRoot, "--force");

            exitCode.Should().Be(0);
            (await File.ReadAllTextAsync(installedPath)).Should().Be("new theme");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task InstallCommand_RejectsInvalidVisualStudioLayout()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string inputPath = Path.Combine(directory, "Sample.pkgdef");
            await File.WriteAllTextAsync(inputPath, "theme");
            string visualStudioRoot = Path.Combine(directory, "VisualStudio");
            Directory.CreateDirectory(visualStudioRoot);

            int exitCode = await RunProgramAsync("install", inputPath, "--target-vs", visualStudioRoot);

            exitCode.Should().NotBe(0);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateVisualStudioLayout(string directory)
    {
        string visualStudioRoot = Path.Combine(directory, "VisualStudio");
        string ideDirectory = Path.Combine(visualStudioRoot, "Common7", "IDE");
        Directory.CreateDirectory(Path.Combine(ideDirectory, "CommonExtensions", "Platform"));
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), Path.Combine(ideDirectory, "devenv.exe"));
        return visualStudioRoot;
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ThemeConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static Task<int> RunProgramAsync(params string[] arguments)
    {
        Type programType = typeof(Converter).Assembly.GetType("ThemeConverter.Program", true)!;
        MethodInfo main = programType.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (Task<int>)main.Invoke(null, [arguments])!;
    }
}

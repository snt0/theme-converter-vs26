// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace ThemeConverterTests;

public class CommandInterfaceTest
{
    private static readonly string ExecutablePath = Path.Combine(AppContext.BaseDirectory, "ThemeConverter.exe");

    [Fact]
    public async Task RootHelp_ListsSeparateWorkflows()
    {
        CommandResult result = await RunAsync("--help");

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

            CommandResult result = await RunAsync("convert", inputPath, "--output", outputPath);

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

            CommandResult result = await RunAsync("install", inputPath, "--target-vs", visualStudioRoot);

            result.ExitCode.Should().NotBe(0);
            result.Output.Should().Contain("--force");
            (await File.ReadAllTextAsync(Path.Combine(themeDirectory, "Sample.pkgdef"))).Should().Be("existing");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static async Task<CommandResult> RunAsync(params string[] arguments)
    {
        ProcessStartInfo startInfo = new(ExecutablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start CLI.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new CommandResult(process.ExitCode, await standardOutput + await standardError);
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ThemeConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed record CommandResult(int ExitCode, string Output);
}

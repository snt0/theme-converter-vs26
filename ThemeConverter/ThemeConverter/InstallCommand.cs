// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ThemeConverter;

internal sealed class InstallCommand : AsyncCommand<InstallCommand.Settings>
{
    internal sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<PKGDEF>")]
        [Description("A pkgdef theme file, or a folder containing pkgdef theme files.")]
        public string InputPath { get; init; } = string.Empty;

        [CommandOption("-t|--target-vs <PATH>")]
        [Description("The root directory of the Visual Studio installation to modify.")]
        public string TargetVs { get; init; } = string.Empty;

        [CommandOption("-f|--force")]
        [Description("Replace theme files that already exist in the target installation.")]
        public bool Force { get; init; }

        [CommandOption("--launch")]
        [Description("Launch Visual Studio after updating its configuration.")]
        public bool Launch { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(InputPath) || (!File.Exists(InputPath) && !Directory.Exists(InputPath)))
            {
                return ValidationResult.Error($"Could not find pkgdef input file or folder: \"{InputPath}\".");
            }

            if (File.Exists(InputPath) && !VisualStudioThemeInstaller.IsPkgDefFile(InputPath))
            {
                return ValidationResult.Error("The install input file must have a .pkgdef extension.");
            }

            if (string.IsNullOrWhiteSpace(TargetVs) || !Directory.Exists(TargetVs))
            {
                return ValidationResult.Error(
                    $"The Visual Studio installation directory \"{TargetVs}\" does not exist.");
            }

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context,
                                                    Settings settings,
                                                    CancellationToken cancellationToken)
    {
        IReadOnlyList<string> pkgdefFiles = Directory.Exists(settings.InputPath)
            ?
            [
                .. Directory.EnumerateFiles(settings.InputPath).Where(VisualStudioThemeInstaller.IsPkgDefFile)
                            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            ]
            : [settings.InputPath];

        if (pkgdefFiles.Count == 0)
        {
            throw new InvalidOperationException("No pkgdef theme files were found in the input folder.");
        }

        IReadOnlyList<string> installedFiles = await VisualStudioThemeInstaller.InstallAsync(pkgdefFiles,
            settings.TargetVs, settings.Force, settings.Launch, cancellationToken);

        foreach (string installedFile in installedFiles)
        {
            AnsiConsole.MarkupLine($"Installed [green]{Markup.Escape(installedFile)}[/]");
        }

        return 0;
    }
}
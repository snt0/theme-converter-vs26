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

internal sealed class ConvertCommand(IAnsiConsole console) : AsyncCommand<ConvertCommand.Settings>
{
    internal sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<INPUT>")]
        [Description("The theme JSON file, or a folder containing theme JSON files.")]
        public string InputPath { get; init; } = string.Empty;

        [CommandOption("-o|--output <PATH>")]
        [Description("The output folder. Defaults to the input file's folder.")]
        public string? OutputPath { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(InputPath))
            {
                return ValidationResult.Error("No input file or folder provided.");
            }

            if (!File.Exists(InputPath) && !Directory.Exists(InputPath))
            {
                return ValidationResult.Error($"Could not find input file or folder: \"{InputPath}\".");
            }

            if (File.Exists(InputPath) && !IsThemeFile(InputPath))
            {
                return ValidationResult.Error("The input file must have a .json or .jsonc extension.");
            }

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context,
                                                    Settings settings,
                                                    CancellationToken cancellationToken)
    {
        Converter.ValidateDataFiles(console.WriteLine);

        string inputPath = settings.InputPath;
        string outputPath = settings.OutputPath ?? GetDirectoryName(inputPath);
        await ConvertAsync(inputPath, outputPath, cancellationToken);

        return 0;
    }

    private static string GetDirectoryName(string path)
    {
        return Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
    }

    private async Task ConvertAsync(string sourcePath, string pkgdefOutputPath, CancellationToken cancellationToken)
    {
        ICollection<string> sourceFiles = Directory.Exists(sourcePath)
            ?
            [
                .. Directory.EnumerateFiles(sourcePath).Where(IsThemeFile)
                            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            ]
            : [sourcePath];

        if (sourceFiles.Count == 0)
        {
            throw new InvalidOperationException("No JSON or JSONC theme files were found in the input folder.");
        }

        foreach (string sourceFile in sourceFiles)
        {
            console.MarkupLine($"Converting [blue]{Markup.Escape(sourceFile)}[/]");
            console.WriteLine();

            string pkgdefFilePath = await Converter.ConvertFileAsync(sourceFile, pkgdefOutputPath, cancellationToken);
            console.MarkupLine($"Created [green]{Markup.Escape(pkgdefFilePath)}[/]");
        }
    }

    private static bool IsThemeFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jsonc", StringComparison.OrdinalIgnoreCase);
    }
}
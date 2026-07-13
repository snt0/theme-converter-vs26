// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ThemeConverter;

internal static class VisualStudioThemeInstaller
{
    private const string ThemeFolder = @"Common7\IDE\CommonExtensions\Platform";
    private const string VisualStudioExecutable = @"Common7\IDE\devenv.exe";

    public static async Task<IReadOnlyList<string>> InstallAsync(IReadOnlyList<string> pkgdefFiles,
                                                                 string visualStudioRoot,
                                                                 bool overwrite,
                                                                 bool launch,
                                                                 CancellationToken cancellationToken)
    {
        string devenvPath = Path.Combine(visualStudioRoot, VisualStudioExecutable);
        if (!File.Exists(devenvPath))
        {
            throw new FileNotFoundException(
                $"The selected directory is not a Visual Studio installation; devenv.exe was not found at '{devenvPath}'.",
                devenvPath);
        }

        string themeFolder = Path.Combine(visualStudioRoot, ThemeFolder);
        if (!Directory.Exists(themeFolder))
        {
            throw new DirectoryNotFoundException(
                $"The Visual Studio theme directory was not found at '{themeFolder}'.");
        }

        string[] destinationPaths = pkgdefFiles.Select(path => Path.Combine(themeFolder, Path.GetFileName(path)))
                                               .ToArray();
        string? existingFile = overwrite ? null : destinationPaths.FirstOrDefault(File.Exists);
        if (existingFile is not null)
        {
            throw new IOException($"A theme file already exists at '{existingFile}'. Use --force to replace it.");
        }

        for (int index = 0; index < pkgdefFiles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(pkgdefFiles[index], destinationPaths[index], overwrite);
        }

        await UpdateConfigurationAsync(devenvPath, cancellationToken);

        if (launch)
        {
            Process.Start(new ProcessStartInfo(devenvPath) { UseShellExecute = true });
        }

        return destinationPaths;
    }

    internal static bool IsPkgDefFile(string path)
    {
        return Path.GetExtension(path).Equals(".pkgdef", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task UpdateConfigurationAsync(string devenvPath, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(devenvPath) { UseShellExecute = false };
        startInfo.ArgumentList.Add("/updateconfiguration");

        using Process process = Process.Start(startInfo) ??
                                throw new InvalidOperationException($"Failed to start '{devenvPath}'.");
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Visual Studio configuration update failed with exit code {process.ExitCode}.");
        }
    }
}
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Spectre.Console.Cli;

namespace ThemeConverter;

internal sealed class Program
{
    internal static Task<int> Main(string[] args)
    {
        CommandApp app = new();
        app.Configure(Configure);

        return app.RunAsync(args.Length == 0 ? ["--help"] : args);
    }

    internal static void Configure(IConfigurator config)
    {
        config.SetApplicationName("ThemeConverter");
        config.SetApplicationVersion(typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");
        config.AddCommand<ConvertCommand>("convert")
              .WithDescription("Convert VS Code theme files into Visual Studio pkgdef files.");
        config.AddCommand<InstallCommand>("install")
              .WithDescription("Install existing pkgdef theme files into a Visual Studio instance.");
    }
}
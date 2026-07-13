// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ThemeConverterTests;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using ThemeConverter;
using Xunit;

public class ConversionTest
{
    private const string DarkThemeFallback = "{1ded0138-47ce-435e-84ef-9ec1f439b749}";
    private const string LightThemeFallback = "{de3dbbcd-f642-433c-8353-8f1df4370aba}";

    /// <summary>
    /// The minimum set of categories that should be present in the case of
    /// a complete theme successful conversion.
    /// </summary>
    private static readonly string[] ExpectedCategoryNames =
    [
        "Text Editor Language Service Items", "Roslyn Text Editor MEF Items", "Text Editor Text Marker Items",
        "Cider", "CommonControls", "CommonDocument", "Diagnostics", "Environment", "Header", "IntelliTrace",
        "ManifestDesigner", "NewProjectDialog", "NotificationBubble", "PackageManifestEditor", "ProjectDesigner",
        "SharePointTools", "ThemedDialog", "TreeView", "UserNotifications", "VisualStudioInstaller", "VSSearch",
        "Find", "Output Window", "StartPage", "ThemedUtilityDialog", "Text Editor Text Manager Items",
        "WebClient Diagnostic Tools", "UserInformation", "InfoBar", "ClientDiagnosticsMemory", "CodeSenseControls",
        "GraphDocumentColors", "GraphicsDesigners", "InformationBadge", "Promotion", "TaskRunnerExplorerControls",
        "TeamExplorer", "WelcomeExperience", "ClientDiagnosticsTimeline", "SearchControl", "ACDCOverview",
        "Editor Tooltip", "CodeSense", "Command Window", "Find Results", "Immediate Window", "Locals",
        "Package Manager Console", "Performance Tips", "Watch", "ApplicationInsights", "ClientDiagnosticsTools",
        "DetailsView", "DiagnosticsHub", "NavigateTo", "VersionControl", "WorkItemEditor", "ProgressBar",
        "UnthemedDialog", "Shell", "ShellInternal"
    ];

    private static readonly Regex GuidRegex = new("[({][a-fA-F0-9]{8}-([a-fA-F0-9]{4}-){3}[a-fA-F0-9]{12}[})]",
        RegexOptions.Compiled);

    private static readonly Regex DataRegex = new("^\"Data\"=hex:[a-fA-F0-9]+(,[a-fA-F0-9]+)*", RegexOptions.Compiled);

    private static readonly string ResultsFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "TestResults",
        DateTime.Now.ToString("yyyy-MM-dd-HHmmss"));

    private static readonly string ThemesFolderPath =
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "TestFiles");

    [Fact]
    public async Task Incomplete_NoColors()
    {
        string pkgdefPath = await ConvertTheme("Incomplete_NoColors.json");
        File.Exists(pkgdefPath).Should().BeTrue();

        string[] lines = await File.ReadAllLinesAsync(pkgdefPath);
        ValidateGeneralThemeInformation(lines, "Incomplete_NoColors", LightThemeFallback);

        lines.Count(l => l.Contains("\"Data\"=hex:")).Should().Be(2,
            "the Fluent Shell and ShellInternal compatibility categories are always emitted");
    }

    [Fact]
    public async Task Incomplete_MissingCriticalColors()
    {
        string pkgdefPath = await ConvertTheme("Incomplete_MissingCriticalColors.json");
        File.Exists(pkgdefPath).Should().BeTrue();

        string[] lines = await File.ReadAllLinesAsync(pkgdefPath);
        ValidateGeneralThemeInformation(lines, "Incomplete_MissingCriticalColors", LightThemeFallback);

        lines.Count(l => l.Contains("\"Data\"=hex:")).Should().Be(3,
            "one mapped category plus the two Fluent compatibility categories should be emitted");
    }

    [Fact]
    public Task Complete_Dark()
    {
        return ConvertAndValidateCompleteTheme("Complete_Dark.json", DarkThemeFallback);
    }

    [Fact]
    public Task Complete_Light()
    {
        return ConvertAndValidateCompleteTheme("Complete_Light.json", LightThemeFallback);
    }

    [Fact]
    public async Task RepeatedConversion_UsesStableIdentityAndOutput()
    {
        string sourcePath = Path.Combine(ThemesFolderPath, "Complete_Dark.json");
        string firstPath = await Converter.ConvertFileAsync(sourcePath, Path.Combine(ResultsFolderPath, "first"));
        string secondPath = await Converter.ConvertFileAsync(sourcePath, Path.Combine(ResultsFolderPath, "second"));

        byte[] first = await File.ReadAllBytesAsync(firstPath);
        byte[] second = await File.ReadAllBytesAsync(secondPath);
        first.Should().Equal(second);

        string[] lines = await File.ReadAllLinesAsync(firstPath);
        string themeId = ValidateGeneralThemeInformation(lines, "Complete_Dark", DarkThemeFallback);
        themeId.Should().Be("{1d03e6d8-abcf-8d03-9b42-2b41f8067b27}");
        Guid.Parse(themeId).Version.Should().Be(8);
    }

    [Fact]
    public void FluentTokenCatalog_IsComplete()
    {
        string catalogPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Data",
            "FluentTokenDefaults.json");
        using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));

        catalog.RootElement.GetProperty("Shell").EnumerateObject().Should().HaveCount(94);
        catalog.RootElement.GetProperty("ShellInternal").EnumerateObject().Should().HaveCount(28);
    }

    [Fact]
    public async Task FluentTokens_LightTheme_AreWrittenToPkgdef()
    {
        string pkgdefPath = await ConvertTheme("Complete_Light.json");
        await ValidateFluentTokenOutput(pkgdefPath, 0xFF5649B0, 0xE55649B0, 0x80FFFFFF, 0x00000000);
    }

    [Fact]
    public async Task FluentTokens_DarkTheme_AreWrittenToPkgdef()
    {
        string pkgdefPath = await ConvertTheme("Complete_Dark.json");
        await ValidateFluentTokenOutput(pkgdefPath, 0xFF9184EE, 0xE59184EE, 0x4D3A3A3A, 0x00FFFFFF);
    }

    private static async Task ConvertAndValidateCompleteTheme(string testFileName, string themeFallbackGuid)
    {
        string pkgdefPath = await ConvertTheme(testFileName);
        File.Exists(pkgdefPath).Should().BeTrue();

        string themeName = Path.GetFileNameWithoutExtension(testFileName);
        string[] lines = await File.ReadAllLinesAsync(pkgdefPath);

        // We check for a limited set of things to be correct in order to keep the test low maintenance.
        // This unfortunately doesn't include verifying the actual colors in the pkgdef.
        string themeGuid = ValidateGeneralThemeInformation(lines, themeName, themeFallbackGuid);
        ValidateThemeCategories(lines, themeGuid, ExpectedCategoryNames);
    }

    private static string ValidateGeneralThemeInformation(IReadOnlyList<string> lines,
                                                          string themeName,
                                                          string themeFallbackGuid)
    {
        // Extract the theme guid from the first line so we can later search for lines that contain it
        Match m = GuidRegex.Match(lines[0]);
        m.Success.Should().BeTrue();
        string themeGuid = m.Groups[0].Value;

        // Check the general theme information
        lines[0].Should().Be($"[$RootKey$\\Themes\\{themeGuid}]");
        lines[1].Should().Be($"@=\"{themeName}\"");
        lines[2].Should().Be($"\"Name\"=\"{themeName}\"");
        lines[3].Should().Be($"\"FallbackId\"=\"{themeFallbackGuid}\"");

        return themeGuid;
    }

    private static void ValidateThemeCategories(string[] lines, string themeGuid, IEnumerable<string> categoryNames)
    {
        // Check that all expected categories are found
        foreach (string categoryName in categoryNames)
        {
            // Ensure category line is present
            string? categoryLine = lines.SingleOrDefault(l => l == $"[$RootKey$\\Themes\\{themeGuid}\\{categoryName}]");
            categoryLine.Should().NotBeNull($"{categoryName} not found");
            int categoryLineIndex = Array.IndexOf(lines, categoryLine!);

            // Next line is data line
            string dataLine = lines[categoryLineIndex + 1];
            Match m = DataRegex.Match(dataLine);
            m.Success.Should().BeTrue();
        }
    }

    private static Task<string> ConvertTheme(string testFileName)
    {
        return Converter.ConvertFileAsync(Path.Combine(ThemesFolderPath, testFileName), ResultsFolderPath);
    }

    private static async Task ValidateFluentTokenOutput(string pkgdefPath,
                                                        uint expectedAccent,
                                                        uint expectedAccentHover,
                                                        uint expectedLayeredBackground,
                                                        uint expectedTransparentFill)
    {
        string[] lines = await File.ReadAllLinesAsync(pkgdefPath);
        PkgdefCategory shell = ReadCategory(lines, "Shell");
        PkgdefCategory shellInternal = ReadCategory(lines, "ShellInternal");

        shell.Id.Should().Be(Guid.Parse("73708ded-2d56-4aad-b8eb-73b20d3f4bff"));
        shellInternal.Id.Should().Be(Guid.Parse("5af241b7-5627-4d12-bfb1-2b67d11127d7"));

        using JsonDocument catalog = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Data", "FluentTokenDefaults.json")));
        string[] expectedShellNames = catalog.RootElement.GetProperty("Shell").EnumerateObject()
                                             .Select(property => property.Name).ToArray();
        string[] expectedShellInternalNames = catalog.RootElement.GetProperty("ShellInternal").EnumerateObject()
                                                     .Select(property => property.Name).ToArray();

        shell.Colors.Keys.Should().BeEquivalentTo(expectedShellNames);
        shellInternal.Colors.Keys.Should().BeEquivalentTo(expectedShellInternalNames);
        shell.Colors["AccentFillDefault"].Should().Be(expectedAccent);
        shell.Colors["AccentFillSecondary"].Should().Be(expectedAccentHover);
        shellInternal.Colors["EnvironmentLayeredBackground"].Should().Be(expectedLayeredBackground);
        shell.Colors["SubtleFillDisabled"].Should().Be(expectedTransparentFill);
    }

    private static PkgdefCategory ReadCategory(string[] lines, string categoryName)
    {
        int sectionIndex = Array.FindIndex(lines,
            line => line.StartsWith("[$RootKey$\\Themes\\", StringComparison.Ordinal) &&
                    line.EndsWith($"\\{categoryName}]", StringComparison.Ordinal));
        sectionIndex.Should().BeGreaterOrEqualTo(0, $"the {categoryName} category should exist");

        string dataLine = lines[sectionIndex + 1];
        const string prefix = "\"Data\"=hex:";
        dataLine.Should().StartWith(prefix);
        byte[] bytes = dataLine[prefix.Length..].Split(',').Select(value => Convert.ToByte(value, 16)).ToArray();

        using BinaryReader reader = new(new MemoryStream(bytes), Encoding.UTF8);
        reader.ReadInt32().Should().Be(bytes.Length);
        reader.ReadInt32().Should().Be(11);
        reader.ReadInt32().Should().Be(1);
        Guid categoryId = new(reader.ReadBytes(16));
        int colorCount = reader.ReadInt32();
        Dictionary<string, uint> colors = [];

        for (int i = 0; i < colorCount; i++)
        {
            string name = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
            byte backgroundType = reader.ReadByte();
            backgroundType.Should().Be(1, $"{name} should have a raw background color");
            uint background = ConvertAbgrToArgb(reader.ReadUInt32());
            reader.ReadByte().Should().Be(0, $"{name} should not have a foreground color");
            colors.Add(name, background);
        }

        reader.BaseStream.Position.Should().Be(reader.BaseStream.Length);
        return new PkgdefCategory(categoryId, colors);
    }

    private static uint ConvertAbgrToArgb(uint abgr)
    {
        return (abgr & 0xFF00FF00) | ((abgr & 0x00FF0000) >> 16) | ((abgr & 0x000000FF) << 16);
    }

    private sealed record PkgdefCategory(Guid Id, Dictionary<string, uint> Colors);
}

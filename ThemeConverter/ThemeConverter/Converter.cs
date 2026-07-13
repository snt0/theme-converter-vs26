// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ThemeConverter.ColorCompiler;
using ThemeConverter.JSON;

namespace ThemeConverter;

public sealed class Converter
{
    private static readonly Guid DarkThemeId = new("{1ded0138-47ce-435e-84ef-9ec1f439b749}");
    private static readonly Guid LightThemeId = new("{de3dbbcd-f642-433c-8353-8f1df4370aba}");
    private static readonly Guid ThemeIdNamespace = new("{6f4ad65d-ef1f-4c36-93b0-93fff768af0c}");

    private static readonly Lazy<FrozenDictionary<string, ColorKey[]>> ScopeMappings =
        new(() => ParseMapping.CreateScopeMapping().ToFrozenDictionary(StringComparer.Ordinal));

    private static readonly Lazy<FrozenDictionary<string, Guid>> CategoryGuids = new(() =>
        ParseMapping.CreateCategoryGuids()
                    .ToFrozenDictionary(item => item.Key, item => Guid.Parse(item.Value), StringComparer.Ordinal));

    private static readonly Lazy<FrozenDictionary<string, string>> VscTokenFallback =
        new(() => ParseMapping.CreateVscTokenFallback().ToFrozenDictionary(StringComparer.Ordinal));

    private static readonly Lazy<FrozenDictionary<string, (float Opacity, string Background)>> OverlayMappings =
        new(() => ParseMapping.CreateOverlayMapping().ToFrozenDictionary(StringComparer.Ordinal));

    private static readonly Lazy<FluentTokenCatalog> FluentTokens = new(LoadFluentTokenCatalog);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Converts a VS Code theme file to a Visual Studio pkgdef file.
    /// </summary>
    /// <param name="themeJsonFilePath">The VS Code theme json file path.</param>
    /// <param name="pkgdefOutputPath">Output folder path to write the .pkgdef file to.</param>
    /// <returns>
    /// Full path to the theme .pkgdef file created in the <paramref name="pkgdefOutputPath"/> folder.
    /// </returns>
    public static async Task<string> ConvertFileAsync(string themeJsonFilePath,
                                                      string pkgdefOutputPath,
                                                      CancellationToken cancellationToken = default)
    {
        string themeName = Path.GetFileNameWithoutExtension(themeJsonFilePath);

        ThemeFileContract theme = await VsCodeThemeReader.ReadAsync(themeJsonFilePath, cancellationToken);

        // Group colors by category.
        Dictionary<string, Dictionary<string, SettingsContract>> colorCategories = GroupColorsByCategory(theme);
        AddFluentTokenCategories(theme.Type, colorCategories);

        Directory.CreateDirectory(pkgdefOutputPath);
        string pkgdefFilePath = Path.Combine(pkgdefOutputPath, $"{themeName}.pkgdef");
        await CompileVsThemeAsync(themeName, theme, colorCategories, pkgdefFilePath, cancellationToken);

        return pkgdefFilePath;
    }

    #region Compile VS Theme

    /// <summary>
    /// Generate the pkgdef from the theme.
    /// </summary>
    /// <param name="themeName">The name of theme.</param>
    /// <param name="theme">The theme object from the json file.</param>
    /// <param name="colorCategories">Colors grouped by category.</param>
    /// <param name="pkgdefFilePath">Path to the generated pkgdef.</param>
    private static Task CompileVsThemeAsync(string themeName,
                                            ThemeFileContract theme,
                                            Dictionary<string, Dictionary<string, SettingsContract>> colorCategories,
                                            string pkgdefFilePath,
                                            CancellationToken cancellationToken)
    {
        ColorManager manager = new();
        ColorTheme compiledTheme = manager.GetOrCreateTheme(CreateDeterministicThemeId(themeName));
        compiledTheme.Name = themeName;
        compiledTheme.FallbackId = string.Equals(theme.Type, "dark", StringComparison.OrdinalIgnoreCase)
            ? DarkThemeId
            : LightThemeId;

        foreach (KeyValuePair<string, Dictionary<string, SettingsContract>> category in colorCategories)
        {
            ColorCategory compiledCategory = manager.RegisterCategory(CategoryGuids.Value[category.Key], category.Key);

            foreach (KeyValuePair<string, SettingsContract> color in category.Value.Where(color =>
                         color is { Value.Foreground: not null } or { Value.Background: not null }))
            {
                ColorEntry entry = manager.GetOrCreateEntry(compiledTheme.ThemeId,
                    new ColorName(compiledCategory, color.Key));
                SetColorValue(color.Value.Background, value =>
                {
                    entry.BackgroundType = Vscolortype.CT_RAW;
                    entry.BackgroundSource = value;
                });
                SetColorValue(color.Value.Foreground, value =>
                {
                    entry.ForegroundType = Vscolortype.CT_RAW;
                    entry.ForegroundSource = value;
                });
            }
        }

        return new PkgDefWriter(manager).SaveToFileAsync(pkgdefFilePath, cancellationToken);
    }

    private static void SetColorValue(string? color, Action<uint> assign)
    {
        if (color is null)
        {
            return;
        }

        assign(RgbaColor.Parse(color).ToAbgr());
    }

    private static Guid CreateDeterministicThemeId(string themeName)
    {
        Span<byte> namespaceBytes = stackalloc byte[16];
        ThemeIdNamespace.TryWriteBytes(namespaceBytes, true, out _);

        byte[] nameBytes = Encoding.UTF8.GetBytes(themeName.Normalize(NormalizationForm.FormC));
        byte[] input = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(input);
        nameBytes.CopyTo(input, namespaceBytes.Length);

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(input, hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new Guid(hash[..16], true);
    }

    #endregion Compile VS Theme

    #region Translate VS Theme

    /// <summary>
    /// Group converted colors by category.
    /// </summary>
    /// <param name="theme">the theme contract.</param>
    /// <returns>Mapping from Category to Color Tokens</returns>
    private static Dictionary<string, Dictionary<string, SettingsContract>> GroupColorsByCategory(
        ThemeFileContract theme)
    {
        // category -> colorKeyName => color value
        Dictionary<string, Dictionary<string, SettingsContract>> colorCategories = [];
        // category -> colorKeyName -> assigned by VSC token
        Dictionary<string, Dictionary<string, string>> assignBy = [];

        Dictionary<string, bool> keyUsed = [];
        foreach (string key in ScopeMappings.Value.Keys)
        {
            keyUsed.Add(key, false);
        }

        // Add the editor colors
        foreach (RuleContract ruleContract in theme.TokenColorRules)
        {
            foreach (string scopeName in ruleContract.ScopeNames)
            {
                string[] scopes = scopeName.Split(',');
                foreach (string scopeRaw in scopes)
                {
                    string scope = scopeRaw.Trim();
                    foreach (string key in ScopeMappings.Value.Keys.Where(key =>
                                 key.StartsWith(scope, StringComparison.Ordinal) && scope.Length > 0))
                    {
                        if (ScopeMappings.Value.TryGetValue(key, out ColorKey[]? colorKeys))
                        {
                            keyUsed[key] = true;
                            AssignEditorColors(colorKeys, scope, ruleContract, ref colorCategories, ref assignBy);
                        }
                    }
                }
            }
        }

        // for keys that were not used during hierarchical assigning, check if there's any fallback that we can use...
        foreach (string key in keyUsed.Keys.Where(key => !keyUsed[key]))
        {
            if (VscTokenFallback.Value.TryGetValue(key, out string? fallbackToken))
            {
                // if the fallback is foreground, assign it like a shell color
                if (fallbackToken == "foreground" && theme.ColorValues.TryGetValue("foreground", out string? color) &&
                    color is not null)
                {
                    if (ScopeMappings.Value.TryGetValue(key, out ColorKey[]? colorKeys))
                    {
                        AssignShellColors(theme, color, colorKeys, ref colorCategories);
                    }
                }

                foreach (RuleContract ruleContract in theme.TokenColorRules)
                {
                    foreach (string scope in ruleContract.ScopeNames.Select(scopeName => scopeName.Split(','))
                                                         .SelectMany(scopes =>
                                                              scopes.Select(scopeRaw => scopeRaw.Trim()).Where(scope =>
                                                                  fallbackToken.StartsWith(scope,
                                                                      StringComparison.Ordinal) && scope.Length > 0)))
                    {
                        if (ScopeMappings.Value.TryGetValue(key, out ColorKey[]? colorKeys))
                        {
                            AssignEditorColors(colorKeys, scope, ruleContract, ref colorCategories, ref assignBy);
                        }
                    }
                }
            }
        }

        // Add the shell colors
        foreach (KeyValuePair<string, string?> color in theme.ColorValues)
        {
            if (ScopeMappings.Value.TryGetValue(color.Key.Trim(), out ColorKey[]? colorKeyList))
            {
                if (!TryGetColorValue(theme, color.Key, out string? colorValue))
                {
                    continue;
                }

                // calculate the actual border color for editor overlay colors
                if (OverlayMappings.Value.TryGetValue(color.Key, out (float Opacity, string Background) overlay) &&
                    TryGetColorValue(theme, overlay.Background, out string? backgroundColor))
                {
                    colorValue = GetCompoundColor(colorValue!, backgroundColor!, overlay.Opacity);
                }

                AssignShellColors(theme, colorValue!, colorKeyList, ref colorCategories);
            }
        }

        return colorCategories;
    }

    private static bool TryGetColorValue(ThemeFileContract theme, string token, out string? colorValue)
    {
        theme.ColorValues.TryGetValue(token, out colorValue);

        string key = token;

        while (colorValue == null)
        {
            if (VscTokenFallback.Value.TryGetValue(key, out string? fallbackToken))
            {
                key = fallbackToken;
                theme.ColorValues.TryGetValue(key, out colorValue);
            }
            else
            {
                break;
            }
        }

        return colorValue != null;
    }

    /// <summary>
    /// Compute what is the compound color of 2 overlayed colors with transparency
    /// </summary>
    /// <param name="vsOpacity">What is the opacity that VS will use when displaying this color</param>
    /// <param name="vscOpacity">The opacity that VSC will apply to this token under special circumstances.</param>
    /// <returns>Color value for VS</returns>
    private static string GetCompoundColor(string overlayColor,
                                           string baseColor,
                                           float vsOpacity = 1,
                                           float vscOpacity = 1)
    {
        RgbaColor overlay = RgbaColor.Parse(overlayColor);
        RgbaColor background = RgbaColor.Parse(baseColor);
        float overlayA = overlay.Alpha * vscOpacity / 255;
        float baseA = background.Alpha / 255f;

        float r = overlayA / vsOpacity * overlay.Red + (1 - overlayA / vsOpacity) * baseA * background.Red;
        float g = overlayA / vsOpacity * overlay.Green + (1 - overlayA / vsOpacity) * baseA * background.Green;
        float b = overlayA / vsOpacity * overlay.Blue + (1 - overlayA / vsOpacity) * baseA * background.Blue;

        r = Math.Clamp(r, 0, 255);
        g = Math.Clamp(g, 0, 255);
        b = Math.Clamp(b, 0, 255);

        return new RgbaColor((byte)r, (byte)g, (byte)b, byte.MaxValue).ToRgbaHex();
    }

    private static void AssignEditorColors(IEnumerable<ColorKey> colorKeys,
                                           string scope,
                                           RuleContract ruleContract,
                                           ref Dictionary<string, Dictionary<string, SettingsContract>> colorCategories,
                                           ref Dictionary<string, Dictionary<string, string>> assignBy)
    {
        foreach (ColorKey colorKey in colorKeys)
        {
            if (!colorCategories.TryGetValue(colorKey.CategoryName,
                    out Dictionary<string, SettingsContract>? rulesList))
            {
                rulesList = [];
                colorCategories[colorKey.CategoryName] = rulesList;
            }

            if (!assignBy.TryGetValue(colorKey.CategoryName, out Dictionary<string, string>? assignList))
            {
                assignList = [];
                assignBy[colorKey.CategoryName] = assignList;
            }

            if (rulesList.ContainsKey(colorKey.KeyName))
            {
                if (scope.StartsWith(assignList[colorKey.KeyName], StringComparison.Ordinal) &&
                    ruleContract.ColorSettings.Foreground != null)
                {
                    rulesList[colorKey.KeyName] = ruleContract.ColorSettings;
                    assignList[colorKey.KeyName] = scope;
                }
            }
            else
            {
                rulesList.Add(colorKey.KeyName, ruleContract.ColorSettings);
                assignList.Add(colorKey.KeyName, scope);
            }
        }
    }

    private static void AssignShellColors(ThemeFileContract theme,
                                          string colorValue,
                                          IEnumerable<ColorKey> colorKeys,
                                          ref Dictionary<string, Dictionary<string, SettingsContract>> colorCategories)
    {
        foreach (ColorKey colorKey in colorKeys)
        {
            if (colorKey.ForegroundOpacity is not null && colorKey.VscBackground is not null)
            {
                if (TryGetColorValue(theme, colorKey.VscBackground, out string? backgroundColor))
                {
                    colorValue = GetCompoundColor(colorValue, backgroundColor!, 1, colorKey.ForegroundOpacity.Value);
                }
            }

            if (!colorCategories.TryGetValue(colorKey.CategoryName,
                    out Dictionary<string, SettingsContract>? rulesList))
            {
                // token name to colors
                rulesList = [];
                colorCategories[colorKey.CategoryName] = rulesList;
            }

            if (!rulesList.TryGetValue(colorKey.KeyName, out SettingsContract? colorSetting))
            {
                colorSetting = new SettingsContract();
                rulesList.Add(colorKey.KeyName, colorSetting);
            }

            if (colorKey.IsBackground)
            {
                colorSetting.Background = colorValue;
            }
            else
            {
                colorSetting.Foreground = colorValue;
            }
        }
    }

    #endregion Translate VS Theme

    #region Write VS Theme

    public static void ValidateDataFiles(Action<string> reportFunc)
    {
        ParseMapping.CheckDuplicateMapping(reportFunc);
    }

    private static void AddFluentTokenCategories(string? themeType,
                                                 Dictionary<string, Dictionary<string, SettingsContract>>
                                                     colorCategories)
    {
        bool isDark = string.Equals(themeType, "dark", StringComparison.OrdinalIgnoreCase);
        colorCategories["Shell"] = CreateBackgroundTokenCategory(FluentTokens.Value.Shell, isDark);
        colorCategories["ShellInternal"] = CreateBackgroundTokenCategory(FluentTokens.Value.ShellInternal, isDark);
    }

    private static Dictionary<string, SettingsContract> CreateBackgroundTokenCategory(
        Dictionary<string, FluentTokenValue> tokens,
        bool isDark)
    {
        return tokens.ToDictionary(token => token.Key,
            token => new SettingsContract
            {
                Background = ConvertArgbToRgba(isDark ? token.Value.Dark : token.Value.Light)
            });
    }

    private static FluentTokenCatalog LoadFluentTokenCatalog()
    {
        string json = File.ReadAllText(ParseMapping.GetDataFilePath("FluentTokenDefaults.json"));
        return JsonSerializer.Deserialize<FluentTokenCatalog>(json, JsonOptions) ??
               throw new JsonException("FluentTokenDefaults.json must contain the Fluent token catalog.");
    }

    private static string ConvertArgbToRgba(string argb)
    {
        return $"#{argb[2..]}{argb[..2]}";
    }

    private readonly record struct RgbaColor(byte Red, byte Green, byte Blue, byte Alpha)
    {
        public static RgbaColor Parse(string value)
        {
            ReadOnlySpan<char> color = value.AsSpan().Trim();
            if (!color.IsEmpty && color[0] == '#')
            {
                color = color[1..];
            }

            return color.Length switch
                   {
                       3 => new RgbaColor(Expand(color[0]), Expand(color[1]), Expand(color[2]), byte.MaxValue),
                       4 => new RgbaColor(Expand(color[0]), Expand(color[1]), Expand(color[2]), Expand(color[3])),
                       6 => new RgbaColor(ParseByte(color[..2]), ParseByte(color.Slice(2, 2)),
                           ParseByte(color.Slice(4, 2)), byte.MaxValue),
                       8 => new RgbaColor(ParseByte(color[..2]), ParseByte(color.Slice(2, 2)),
                           ParseByte(color.Slice(4, 2)), ParseByte(color.Slice(6, 2))),
                       _ => throw new FormatException(
                           $"'{value}' is not a supported hexadecimal color. Expected RGB, RGBA, RRGGBB, or RRGGBBAA.")
                   };
        }

        public uint ToAbgr()
        {
            return (uint)((Alpha << 24) | (Blue << 16) | (Green << 8) | Red);
        }

        public string ToRgbaHex()
        {
            return string.Create(8, this, static (destination, color) =>
            {
                color.Red.TryFormat(destination[..2], out _, "X2", CultureInfo.InvariantCulture);
                color.Green.TryFormat(destination.Slice(2, 2), out _, "X2", CultureInfo.InvariantCulture);
                color.Blue.TryFormat(destination.Slice(4, 2), out _, "X2", CultureInfo.InvariantCulture);
                color.Alpha.TryFormat(destination.Slice(6, 2), out _, "X2", CultureInfo.InvariantCulture);
            });
        }

        private static byte ParseByte(ReadOnlySpan<char> value)
        {
            return byte.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private static byte Expand(char value)
        {
            int nibble = value switch
                         {
                             >= '0' and <= '9' => value - '0',
                             >= 'a' and <= 'f' => value - 'a' + 10,
                             >= 'A' and <= 'F' => value - 'A' + 10,
                             _ => throw new FormatException($"'{value}' is not a hexadecimal digit.")
                         };
            return (byte)(nibble * 17);
        }
    }

    #endregion Write VS Theme
}

internal sealed record ColorKey
{
    public ColorKey(string categoryName,
                    string keyName,
                    string backgroundOrForeground,
                    string? foregroundOpacity = null,
                    string? vscBackground = null)
    {
        CategoryName = categoryName;
        KeyName = keyName;
        Aspect = backgroundOrForeground;

        IsBackground = backgroundOrForeground.Equals("Background", StringComparison.OrdinalIgnoreCase);

        ForegroundOpacity = foregroundOpacity == null
            ? null
            : float.Parse(foregroundOpacity, CultureInfo.InvariantCulture.NumberFormat);
        VscBackground = vscBackground;
    }

    public string CategoryName { get; }

    public string KeyName { get; }

    public string Aspect { get; }

    public bool IsBackground { get; }

    public float? ForegroundOpacity { get; }

    public string? VscBackground { get; }

    public override string ToString()
    {
        return CategoryName + "&" + KeyName + "&" + Aspect;
    }
}

internal sealed record FluentTokenCatalog
{
    public Dictionary<string, FluentTokenValue> Shell { get; init; } = [];

    public Dictionary<string, FluentTokenValue> ShellInternal { get; init; } = [];
}

internal sealed record FluentTokenValue
{
    public required string Light { get; init; }

    public required string Dark { get; init; }
}
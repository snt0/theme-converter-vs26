// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ThemeConverter.ColorCompiler;

/// <summary>
/// Writes a ColorManager out to a pkgdef file.
/// </summary>
internal sealed class PkgDefWriter(ColorManager manager)
{
    public async Task SaveToFileAsync(string fileName, CancellationToken cancellationToken)
    {
        EnsureDirectoryExists(Path.GetDirectoryName(fileName));

        await using PkgDefFileWriter writer = new(fileName, true);
        await WriteRegistrationAsync(writer, cancellationToken);
        await WriteThemesAsync(writer, cancellationToken);
    }

    private async Task WriteRegistrationAsync(PkgDefFileWriter writer, CancellationToken cancellationToken)
    {
        PkgDefItem item = new();
        foreach (ColorTheme theme in manager.Themes)
        {
            item.SectionName = string.Format(CultureInfo.InvariantCulture, @"$RootKey$\Themes\{0:B}", theme.ThemeId);
            item.ValueDataType = PkgDefValueType.String;

            item.ValueName = "@";
            item.ValueDataString = theme.Name;
            await writer.WriteAsync(item, cancellationToken);

            item.ValueName = "Name";
            item.ValueDataString = theme.Name;
            await writer.WriteAsync(item, cancellationToken);

            if (theme.FallbackId != Guid.Empty)
            {
                item.ValueName = nameof(theme.FallbackId);
                item.ValueDataString = theme.FallbackId.ToString("B");
                await writer.WriteAsync(item, cancellationToken);
            }
        }
    }

    private async Task WriteThemesAsync(PkgDefFileWriter writer, CancellationToken cancellationToken)
    {
        Dictionary<CategoryThemeKey, List<ColorEntry>> entries = [];

        foreach (ColorEntry entry in manager.Themes.SelectMany(t => t.Colors))
        {
            CategoryThemeKey key = new(entry.Name.Category.Id, entry.Theme!.ThemeId);
            if (!entries.TryGetValue(key, out List<ColorEntry>? keyEntries))
            {
                keyEntries = [];
                entries[key] = keyEntries;
            }

            keyEntries.Add(entry);
        }

        PkgDefItem item = new();

        foreach (KeyValuePair<CategoryThemeKey, List<ColorEntry>> unsavedSet in entries)
        {
            ColorCategory category = manager.CategoryIndex[unsavedSet.Key.Category];
            item.SectionName = string.Format(CultureInfo.InvariantCulture, @"$RootKey$\Themes\{0:B}\{1}",
                unsavedSet.Key.ThemeId, category.Name);
            item.ValueName = null;
            item.ValueDataType = PkgDefValueType.String;
            await writer.WriteAsync(item, cancellationToken);

            item.ValueName = "Data";
            item.ValueDataType = PkgDefValueType.Binary;
            PopulateThemeCategoryInfo(ref item, category.Id, unsavedSet.Value);
            await writer.WriteAsync(item, cancellationToken);
        }
    }

    private static void EnsureDirectoryExists(string? directory)
    {
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void PopulateThemeCategoryInfo(ref PkgDefItem item,
                                                  Guid category,
                                                  IEnumerable<ColorEntry> colorEntries)
    {
        using MemoryStream stream = new();
        using (VersionedBinaryWriter versionedWriter = new(stream))
        {
            CategoryCollectionRecord record = new();

            CategoryRecord categoryRecord = new(category);
            record.Categories.Add(categoryRecord);

            foreach (ColorRecord colorRecord in colorEntries.Select(CreateColorRecord))
            {
                categoryRecord.Colors.Add(colorRecord);
            }

            versionedWriter.WriteVersioned(PkgDefConstants.ExpectedVersion,
                (checkedWriter, _) => { record.Write(checkedWriter); });
        }

        item.ValueDataBinary = stream.ToArray();
    }

    private static ColorRecord CreateColorRecord(ColorEntry entry)
    {
        ColorRecord colorRecord = new(entry.Name.Name)
        {
            BackgroundType = entry.BackgroundType,
            Background = entry.BackgroundSource,
            ForegroundType = entry.ForegroundType,
            Foreground = entry.ForegroundSource
        };
        return colorRecord;
    }
}

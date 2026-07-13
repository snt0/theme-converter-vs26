// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace ThemeConverter.ColorCompiler;

internal sealed class ColorManager
{
    public ColorManager()
    {
        Themes = new ColorThemeCollection(this);
    }

    public ColorTheme GetOrCreateTheme(Guid themeId)
    {
        if (ThemeIndex.TryGetValue(themeId, out ColorTheme? theme))
        {
            return theme;
        }

        theme = new ColorTheme(themeId);
        Themes.Add(theme);
        return theme;
    }

    public IDictionary<Guid, ColorTheme> ThemeIndex { get; } = new Dictionary<Guid, ColorTheme>();

    public IList<ColorCategory> Categories { get; } = [];

    public IList<ColorTheme> Themes { get; }

    public IDictionary<Guid, ColorCategory> CategoryIndex { get; } = new Dictionary<Guid, ColorCategory>();

    public ColorEntry GetOrCreateEntry(Guid themeId, ColorName name)
    {
        ColorTheme theme = GetOrCreateTheme(themeId);
        if (theme.Index.TryGetValue(name, out ColorEntry? entry))
        {
            return entry;
        }

        entry = new ColorEntry(name);
        theme.Colors.Add(entry);

        return entry;
    }

    public ColorCategory RegisterCategory(Guid categoryId, string name)
    {
        if (CategoryIndex.TryGetValue(categoryId, out ColorCategory category))
        {
            return category;
        }

        category = new ColorCategory(categoryId, name);
        CategoryIndex[categoryId] = category;
        Categories.Add(category);

        return category;
    }

    private sealed class ColorThemeCollection(ColorManager manager) : OwnershipCollection<ColorTheme>
    {
        protected override void TakeOwnership(ColorTheme item)
        {
            manager.ThemeIndex[item.ThemeId] = item;
        }

        protected override void LoseOwnership(ColorTheme item)
        {
            manager.ThemeIndex.Remove(item.ThemeId);
        }
    }
}

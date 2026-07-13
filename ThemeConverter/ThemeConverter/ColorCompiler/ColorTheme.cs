// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace ThemeConverter.ColorCompiler;

internal sealed class ColorTheme(Guid themeId)
{
    public Guid ThemeId { get; private set; } = themeId;

    public string? Name { get; set; }

    public Guid FallbackId { get; set; }

    public IList<ColorEntry> Colors
    {
        get { return field ??= new ColorEntryCollection(this); }
    }

    public IDictionary<ColorName, ColorEntry> Index { get; } = new Dictionary<ColorName, ColorEntry>();

    private sealed class ColorEntryCollection(ColorTheme theme) : OwnershipCollection<ColorEntry>
    {
        protected override void TakeOwnership(ColorEntry item)
        {
            if (item.Theme != null)
            {
                throw new InvalidOperationException("Color entry can only belong to one theme");
            }

            item.Theme = theme;
            theme.Index[item.Name] = item;
        }

        protected override void LoseOwnership(ColorEntry item)
        {
            theme.Index.Remove(item.Name);
            item.Theme = null;
        }
    }
}

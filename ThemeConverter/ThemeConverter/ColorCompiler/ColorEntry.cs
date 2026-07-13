// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ThemeConverter.ColorCompiler;

internal sealed class ColorEntry(ColorName name)
{
    public ColorName Name { get; } = name;

    public ColorTheme? Theme { get; set; }

    public uint BackgroundSource { get; set; }

    public Vscolortype BackgroundType { get; set; }

    public uint ForegroundSource { get; set; }

    public Vscolortype ForegroundType { get; set; }
}
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ThemeConverter.ColorCompiler;

/// <summary>
/// Represents an item in a pkgdef file.
/// </summary>
internal record struct PkgDefItem
{
    public string? SectionName { get; set; }

    public string? ValueName { get; set; }

    public PkgDefValueType ValueDataType { get; set; }

    public string? ValueDataString { get; set; }

    public byte[]? ValueDataBinary { get; set; }

    public override string ToString()
    {
        return string.IsNullOrEmpty(ValueName) ? $"[{SectionName}]" : $"[{SectionName}] {ValueName}=???";
    }
}

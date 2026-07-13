// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;

namespace ThemeConverter.ColorCompiler;

/// <summary>
/// Reads or writes a category of colors from a binary stream.  Each
/// category record contains the GUID identifier for the category
/// and the sequence of color names and values contained in the category.
/// </summary>
internal sealed class CategoryRecord(Guid category)
{
    public IList<ColorRecord> Colors { get; } = [];

    public void Write(BinaryWriter writer)
    {
        writer.Write(category.ToByteArray());
        writer.Write(Colors.Count);
        foreach (ColorRecord entry in Colors)
        {
            entry.Write(writer);
        }
    }
}

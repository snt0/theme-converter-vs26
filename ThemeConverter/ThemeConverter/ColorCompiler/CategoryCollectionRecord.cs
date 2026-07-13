// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;

namespace ThemeConverter.ColorCompiler;

/// <summary>
/// Reads or writes a sequence of category records from a binary stream.
/// A CategoryCollectionRecord contains a sequence of CategoryRecords.
/// Each CategoryRecord contains a sequence of ColorRecords, and each
/// ColorRecord specifies a name and values for a single color.
/// CategoryCollectionRecords are merged together by a ColorTheme to
/// form the full theme.
/// </summary>
internal sealed class CategoryCollectionRecord
{
    public IList<CategoryRecord> Categories { get; } = [];

    public void Write(BinaryWriter writer)
    {
        writer.Write(Categories.Count);

        foreach (CategoryRecord theme in Categories)
        {
            theme.Write(writer);
        }
    }
}
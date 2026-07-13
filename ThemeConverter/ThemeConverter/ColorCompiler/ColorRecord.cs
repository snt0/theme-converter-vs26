// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Text;

namespace ThemeConverter.ColorCompiler;

/// <summary>
/// Reads or writes the value of a color from a binary stream.  The color record
/// captures the string name of the color and its default background and foreground
/// values.  The ColorRecord must be scoped within a CategoryRecord to fully-identify
/// the color's name.
/// </summary>
internal sealed class ColorRecord
{
    private readonly string _name;

    public Vscolortype BackgroundType { get; set; }

    public Vscolortype ForegroundType { get; set; }

    public uint Background { get; set; }

    public uint Foreground { get; set; }

    public ColorRecord(string name)
    {
        _name = name;
    }

    public ColorRecord(BinaryReader reader)
    {
        int nameLength = reader.ReadInt32();
        _name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));

        BackgroundType = (Vscolortype)reader.ReadByte();
        if (IsValidColorType(BackgroundType))
        {
            Background = reader.ReadUInt32();
        }
        else
        {
            BackgroundType = (byte)Vscolortype.CT_INVALID;
            Background = 0;
        }

        ForegroundType = (Vscolortype)reader.ReadByte();
        if (IsValidColorType(ForegroundType))
        {
            Foreground = reader.ReadUInt32();
        }
        else
        {
            ForegroundType = (byte)Vscolortype.CT_INVALID;
            Foreground = 0;
        }
    }

    public void Write(BinaryWriter writer)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(_name);
        writer.Write(nameBytes.Length);
        writer.Write(nameBytes);

        writer.Write((byte)BackgroundType);
        if (IsValidColorType(BackgroundType))
        {
            writer.Write(Background);
        }

        writer.Write((byte)ForegroundType);
        if (IsValidColorType(ForegroundType))
        {
            writer.Write(Foreground);
        }
    }

    private static bool IsValidColorType(Vscolortype colorType)
    {
        return colorType is Vscolortype.CT_RAW or Vscolortype.CT_SYSCOLOR or Vscolortype.CT_AUTOMATIC
            or Vscolortype.CT_COLORINDEX;
    }
}
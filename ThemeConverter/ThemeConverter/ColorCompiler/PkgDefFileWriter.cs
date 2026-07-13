// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThemeConverter.ColorCompiler;

internal sealed class PkgDefFileWriter : IAsyncDisposable
{
    private readonly StreamWriter _file;
    private string _lastSectionWritten = string.Empty;

    public PkgDefFileWriter(string filePath, bool overwriteExisting)
    {
        FileMode mode = overwriteExisting ? FileMode.Create : FileMode.Append;
        FileStream stream = new(filePath, mode, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
        _file = new StreamWriter(stream, Encoding.UTF8);
    }

    private static class Constants
    {
        public const string SectionStartChar = @"[";
        public const string SectionEndChar = @"]";
        public const string BinaryPrefix = "hex:";
    }

    public async ValueTask WriteAsync(PkgDefItem item, CancellationToken cancellationToken)
    {
        if (item.SectionName is "" or null)
        {
            return;
        }

        if (item.SectionName != _lastSectionWritten)
        {
            if (_lastSectionWritten != string.Empty)
            {
                await _file.WriteLineAsync(ReadOnlyMemory<char>.Empty, cancellationToken);
            }

            string line = $"{Constants.SectionStartChar}{item.SectionName}{Constants.SectionEndChar}";
            await _file.WriteLineAsync(line.AsMemory(), cancellationToken);
            _lastSectionWritten = item.SectionName;
        }

        if (string.IsNullOrEmpty(item.ValueName))
        {
            return;
        }

        {
            if (item.ValueName == "@")
            {
                await _file.WriteAsync(item.ValueName.AsMemory(), cancellationToken);
            }
            else
            {
                string line = $"\"{item.ValueName}\"";
                await _file.WriteAsync(line.AsMemory(), cancellationToken);
            }

            await _file.WriteAsync("=".AsMemory(), cancellationToken);

            switch (item.ValueDataType)
            {
                case PkgDefValueType.String:
                {
                    string line = $"\"{item.ValueDataString}\"";
                    await _file.WriteAsync(line.AsMemory(), cancellationToken);
                    break;
                }
                case PkgDefValueType.Binary:
                {
                    string line = $"{Constants.BinaryPrefix}{DataToHexString(item.ValueDataBinary!)}";
                    await _file.WriteAsync(line.AsMemory(), cancellationToken);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(item), item.ValueDataType,
                        "ValueDataType was out of range");
            }

            await _file.WriteLineAsync(ReadOnlyMemory<char>.Empty, cancellationToken);
        }
    }

    private static string DataToHexString(byte[] binaryData)
    {
        if (binaryData.Length == 0)
        {
            return string.Empty;
        }

        return string.Create(binaryData.Length * 3 - 1, binaryData, static (destination, data) =>
        {
            for (int index = 0; index < data.Length; index++)
            {
                if (index > 0)
                {
                    destination[index * 3 - 1] = ',';
                }

                data[index].TryFormat(destination.Slice(index * 3, 2), out _, "x2", CultureInfo.InvariantCulture);
            }
        });
    }

    public ValueTask DisposeAsync()
    {
        return _file.DisposeAsync();
    }
}
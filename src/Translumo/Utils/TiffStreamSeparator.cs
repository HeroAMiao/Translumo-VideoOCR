using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Translumo.Utils
{

    class TiffStreamSeparator
    {
        private static readonly byte[] Header = { 0x49, 0x49, 0x2A, 0x00 };
        private readonly MemoryStream _tiffBuffer = new();
        private const ushort StripOffsetsTag = 273;
        private const ushort StripByteCountsTag = 279;

        private async Task<bool> TryRead(Stream input, int size, CancellationToken cancellationToken)
        {
            if (_tiffBuffer.Length >= size)
            {
                return true;
            }

            ExpandTiffBuffer(size);
            var realLength = _tiffBuffer.Length;
            _tiffBuffer.SetLength(size);
            while (realLength < size)
            {
                var read = await input
                    .ReadAsync(_tiffBuffer.GetBuffer().AsMemory((int)realLength, (int)(size - realLength)),
                        cancellationToken).ConfigureAwait(false);
                realLength += read;
                if (read == 0)
                {
                    _tiffBuffer.SetLength(realLength);
                    return false;
                }
            }

            return true;
        }

        private void ExpandTiffBuffer(int size)
        {
            _tiffBuffer.Capacity = Math.Max(_tiffBuffer.Capacity, size);
        }

        private async Task EnsureDataRead(Stream input, int size, CancellationToken cancellationToken)
        {
            if (_tiffBuffer.Length >= size)
            {
                return;
            }

            _tiffBuffer.Capacity = Math.Max(_tiffBuffer.Capacity, size);
            var start = _tiffBuffer.Length;
            _tiffBuffer.SetLength(size);
            await input.ReadExactlyAsync(_tiffBuffer.GetBuffer().AsMemory((int)start, (int)(size - start)),
                cancellationToken).ConfigureAwait(false);
        }

        public byte[] GetTiff()
        {
            return _tiffBuffer.ToArray();
        }

        public async Task<bool> TryReadOneTiff(Stream input, CancellationToken cancellationToken = default)
        {
            _tiffBuffer.Position = 0;
            _tiffBuffer.SetLength(0);
            ExpandTiffBuffer(8);
            if (!await TryRead(input, 8, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            if (!Header.SequenceEqual(_tiffBuffer.GetBuffer().Take(Header.Length)))
            {
                throw new TiffException("Invalid TIFF header");
            }

            var ifdOffset = (int)BitConverter.ToUInt32(_tiffBuffer.GetBuffer(), 4);
            var max = await ReadIFDs(input, ifdOffset, 0, cancellationToken);
            await EnsureDataRead(input, max, cancellationToken);
            return true;
        }

        private int GetMaxPointer(byte[] buf, int entryStart)
        {
            var tagType = BitConverter.ToUInt16(buf, entryStart + 2);
            var tagCountValues = BitConverter.ToUInt32(buf, entryStart + 4);
            var tagValueOffset = BitConverter.ToUInt32(buf, entryStart + 8);
            int valueSize;
            switch (tagType)
            {
                case 1: //byte
                case 2: //ascii
                    valueSize = 1;
                    break;
                case 3: //short
                    valueSize = 2;
                    break;
                case 4: //long
                    valueSize = 4;
                    break;
                case 5:
                    valueSize = 8;
                    break;
                default:
                    throw new TiffException($"unknown tag type {tagType} at {entryStart + 2}");
            }

            if (valueSize * tagCountValues <= 4)
            {
                return entryStart + 12;
            }
            else
            {
                return (int)(tagValueOffset + valueSize * tagCountValues);
            }
        }

        private int ParseUint32(byte[] buf, int pos)
        {
            return (int)BitConverter.ToUInt32(buf, pos);
        }

        private int ParseUint16(byte[] buf, int pos)
        {
            return BitConverter.ToUInt16(buf, pos);
        }

        private async Task<int> ReadIFDs(Stream input, int ifdOffset, int currentMax,
            CancellationToken cancellationToken = default)
        {
            while (true)
            {
                await EnsureDataRead(input, ifdOffset + 2, cancellationToken).ConfigureAwait(false);
                var entryCount = BitConverter.ToUInt16(_tiffBuffer.GetBuffer(), ifdOffset);
                currentMax = Math.Max(ifdOffset + 2 + entryCount * 12 + 4, currentMax);
                await EnsureDataRead(input, ifdOffset + 2 + entryCount * 12 + 4, cancellationToken)
                    .ConfigureAwait(false);
                var buf = _tiffBuffer.GetBuffer();
                int[] stripOffsets = null;
                int[] stripBytes = null;
                for (var i = 0; i < entryCount; i++)
                {
                    var entryStart = ifdOffset + 2 + i * 12;
                    var tagId = BitConverter.ToUInt16(buf, entryStart);
                    var entryMax = GetMaxPointer(buf, entryStart);
                    currentMax = Math.Max(currentMax, entryMax);
                    if (tagId == StripOffsetsTag || tagId == StripByteCountsTag)
                    {
                        var tagType = BitConverter.ToUInt16(buf, entryStart + 2);
                        var tagCountValues = BitConverter.ToUInt32(buf, entryStart + 4);
                        var tagValueOffset = BitConverter.ToUInt32(buf, entryStart + 8);
                        int[] arr = new int[tagCountValues];
                        //value can only be uint16 or uint32.
                        var valueSize = tagType == 3 ? 2 : 4;
                        Func<byte[], int, int> parseFunc = tagType == 3 ? ParseUint16 : ParseUint32;
                        int arrStartOffset;
                        if (valueSize * tagCountValues <= 4)
                        {
                            arrStartOffset = entryStart + 8;
                        }
                        else
                        {
                            await EnsureDataRead(input, (int)(tagValueOffset + valueSize * tagCountValues),
                                cancellationToken).ConfigureAwait(false);
                            buf = _tiffBuffer.GetBuffer();
                            arrStartOffset = (int)tagValueOffset;
                        }

                        for (var j = 0; j < tagCountValues; j++)
                        {
                            arr[j] = parseFunc(buf, arrStartOffset + j * valueSize);
                        }

                        if (tagId == StripOffsetsTag)
                        {
                            stripOffsets = arr;
                        }
                        else
                        {
                            stripBytes = arr;
                        }
                    }
                }

                if (stripBytes == null || stripOffsets == null)
                {
                    throw new TiffException("strip offset or strip bytes not found");
                }

                if (stripBytes.Length != stripOffsets.Length)
                {
                    throw new TiffException("different length of strip offset and strip bytes");
                }

                for (int i = 0; i < stripBytes.Length; i++)
                {
                    var length = stripBytes[i];
                    var offset = stripOffsets[i];
                    var max = offset + length;
                    currentMax = Math.Max(currentMax, max);
                }

                var nextIFD = BitConverter.ToUInt32(buf, entryCount * 12 + ifdOffset + 2);

                if (nextIFD != 0)
                {
                    ifdOffset = (int)nextIFD;
                    continue;
                }

                return currentMax;
            }
        }

        private class TiffException : Exception
        {
            public TiffException()
            {
            }

            public TiffException(string? message) : base(message)
            {
            }

            public TiffException(string? message, Exception? innerException) : base(message, innerException)
            {
            }
        }
    }
}
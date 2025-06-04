#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading.Tasks;

namespace dotnetCampus.FileDownloader.Utils.BreakpointResumptionTransmissions;
internal partial class BreakpointResumptionTransmissionManager
{
    // Copy From dotnet runtime: \src\libraries\System.IO.Hashing\src\System\IO\Hashing\Crc64.cs
    // Licensed to the .NET Foundation under one or more agreements.
    // The .NET Foundation licenses this file to you under the MIT license.
    class Crc64
    {
        private const ulong InitialState = 0UL;
        private const int Size = sizeof(ulong);

        private ulong _crc = InitialState;

        /// <summary>
        ///   Initializes a new instance of the <see cref="Crc64"/> class.
        /// </summary>
        public Crc64()
        {
            _crcLookup = GenerateTable(0x42F0E1EBA9EA3693);
        }

        /// <summary>
        ///   Appends the contents of <paramref name="source"/> to the data already
        ///   processed for the current hash computation.
        /// </summary>
        /// <param name="source">The data to process.</param>
        public ulong Append(ReadOnlySpan<byte> source)
        {
            _crc = Update(_crc, source);
            return _crc;
        }

        /// <summary>
        ///   Resets the hash computation to the initial state.
        /// </summary>
        public void Reset()
        {
            _crc = InitialState;
        }

        /// <summary>
        ///   Writes the computed hash value to <paramref name="destination"/>
        ///   without modifying accumulated state.
        /// </summary>
        /// <param name="destination">The buffer that receives the computed hash value.</param>
        protected void GetCurrentHashCore(Span<byte> destination)
        {
            // The finalization step of the CRC is to perform the ones' complement.
            BinaryPrimitives.WriteUInt64BigEndian(destination, _crc);
        }

        /// <summary>
        ///   Writes the computed hash value to <paramref name="destination"/>
        ///   then clears the accumulated state.
        /// </summary>
        protected void GetHashAndResetCore(Span<byte> destination)
        {
            BinaryPrimitives.WriteUInt64BigEndian(destination, _crc);
            _crc = InitialState;
        }

        /// <summary>
        ///   Computes the CRC-32 hash of the provided data.
        /// </summary>
        /// <param name="source">The data to hash.</param>
        /// <returns>The CRC-32 hash of the provided data.</returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="source"/> is <see langword="null"/>.
        /// </exception>
        public byte[] Hash(byte[] source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            return Hash(new ReadOnlySpan<byte>(source));
        }

        /// <summary>
        ///   Computes the CRC-32 hash of the provided data.
        /// </summary>
        /// <param name="source">The data to hash.</param>
        /// <returns>The CRC-32 hash of the provided data.</returns>
        public byte[] Hash(ReadOnlySpan<byte> source)
        {
            byte[] ret = new byte[Size];
            StaticHash(source, ret);
            return ret;
        }

        /// <summary>
        ///   Attempts to compute the CRC-32 hash of the provided data into the provided destination.
        /// </summary>
        /// <param name="source">The data to hash.</param>
        /// <param name="destination">The buffer that receives the computed hash value.</param>
        /// <param name="bytesWritten">
        ///   On success, receives the number of bytes written to <paramref name="destination"/>.
        /// </param>
        /// <returns>
        ///   <see langword="true"/> if <paramref name="destination"/> is long enough to receive
        ///   the computed hash value (4 bytes); otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryHash(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten)
        {
            if (destination.Length < Size)
            {
                bytesWritten = 0;
                return false;
            }

            bytesWritten = StaticHash(source, destination);
            return true;
        }

        /// <summary>
        ///   Computes the CRC-32 hash of the provided data into the provided destination.
        /// </summary>
        /// <param name="source">The data to hash.</param>
        /// <param name="destination">The buffer that receives the computed hash value.</param>
        /// <returns>
        ///   The number of bytes written to <paramref name="destination"/>.
        /// </returns>
        public int Hash(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (destination.Length < Size)
                throw new ArgumentException("Argument_DestinationTooShort", nameof(destination));

            return StaticHash(source, destination);
        }

        private int StaticHash(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            ulong crc = InitialState;
            crc = Update(crc, source);
            BinaryPrimitives.WriteUInt64BigEndian(destination, crc);
            return Size;
        }

        private  ulong Update(ulong crc, ReadOnlySpan<byte> source)
        {
            for (int i = 0; i < source.Length; i++)
            {
                ulong idx = (crc >> 56);
                idx ^= source[i];
                crc = _crcLookup[idx] ^ (crc << 8);
            }

            return crc;
        }

        // Pre-computed CRC-32 transition table.
        // While this implementation is based on the standard CRC-32 polynomial,
        // x32 + x26 + x23 + x22 + x16 + x12 + x11 + x10 + x8 + x7 + x5 + x4 + x2 + x1 + x0,
        // this version uses reflected bit ordering, so 0x04C11DB7 becomes 0xEDB88320
        private readonly ulong[] _crcLookup;

        private static ulong[] GenerateTable(ulong polynomial)
        {
            ulong[] table = new ulong[256];

            for (int i = 0; i < 256; i++)
            {
                ulong val = (ulong) i << 56;

                for (int j = 0; j < 8; j++)
                {
                    if ((val & 0x8000_0000_0000_0000) == 0)
                    {
                        val <<= 1;
                    }
                    else
                    {
                        val = (val << 1) ^ polynomial;
                    }
                }

                table[i] = val;
            }

            return table;
        }
    }

    static class CrcHelper
    {
        public static async Task<bool> CheckCrcAsync(Stream stream, ulong expectedCrc, long checkLength, ISharedArrayPool sharedArrayPool, int bufferLength)
        {
            var buffer = sharedArrayPool.Rent(bufferLength);
            try
            {
                var crc64 = new Crc64();
                ulong checksum = 0;
                var remainLength = checkLength;
                while (remainLength > 0)
                {
                    var readLength = (int) Math.Min(bufferLength, remainLength);
                    var read = await stream.ReadAsync(buffer, 0, readLength);
                    if (read != readLength)
                    {
                        return false;
                    }

                    checksum = crc64.Append(buffer.AsSpan(0, read));
                    remainLength -= readLength;
                }

                return checksum == expectedCrc;
            }
            finally
            {
                sharedArrayPool.Return(buffer);
            }
        }
    }
}
#endif

#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Buffers.Binary;

namespace dotnetCampus.FileDownloader.Utils.BreakpointResumptionTransmissions;
internal partial class BreakpointResumptionTransmissionManager
{
    // Copy From dotnet runtime: \src\libraries\System.IO.Hashing\src\System\IO\Hashing\Crc32.cs
    // Licensed to the .NET Foundation under one or more agreements.
    // The .NET Foundation licenses this file to you under the MIT license.
    class Crc32
    {
        private const uint InitialState = 0xFFFF_FFFFu;
        private const int Size = sizeof(uint);

        private uint _crc = InitialState;

        /// <summary>
        ///   Initializes a new instance of the <see cref="Crc32"/> class.
        /// </summary>
        public Crc32()
        {
            _crcLookup = GenerateReflectedTable(0xEDB88320u);
        }

        /// <summary>
        ///   Appends the contents of <paramref name="source"/> to the data already
        ///   processed for the current hash computation.
        /// </summary>
        /// <param name="source">The data to process.</param>
        public uint Append(ReadOnlySpan<byte> source)
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
            BinaryPrimitives.WriteUInt32LittleEndian(destination, ~_crc);
        }

        /// <summary>
        ///   Writes the computed hash value to <paramref name="destination"/>
        ///   then clears the accumulated state.
        /// </summary>
        protected void GetHashAndResetCore(Span<byte> destination)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, ~_crc);
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
            uint crc = InitialState;
            crc = Update(crc, source);
            BinaryPrimitives.WriteUInt32LittleEndian(destination, ~crc);
            return Size;
        }

        private uint Update(uint crc, ReadOnlySpan<byte> source)
        {
            for (int i = 0; i < source.Length; i++)
            {
                byte idx = (byte) crc;
                idx ^= source[i];
                crc = _crcLookup[idx] ^ (crc >> 8);
            }

            return crc;
        }

        // Pre-computed CRC-32 transition table.
        // While this implementation is based on the standard CRC-32 polynomial,
        // x32 + x26 + x23 + x22 + x16 + x12 + x11 + x10 + x8 + x7 + x5 + x4 + x2 + x1 + x0,
        // this version uses reflected bit ordering, so 0x04C11DB7 becomes 0xEDB88320
        private readonly uint[] _crcLookup;

        private static uint[] GenerateReflectedTable(uint reflectedPolynomial)
        {
            uint[] table = new uint[256];

            for (int i = 0; i < 256; i++)
            {
                uint val = (uint) i;

                for (int j = 0; j < 8; j++)
                {
                    if ((val & 0b0000_0001) == 0)
                    {
                        val >>= 1;
                    }
                    else
                    {
                        val = (val >> 1) ^ reflectedPolynomial;
                    }
                }

                table[i] = val;
            }

            return table;
        }
    }
}
#endif

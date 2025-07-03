using System.Runtime.CompilerServices;

namespace Protobuf.DynamicJson.Utils;

/// <summary>
/// Provides ZigZag encoding and decoding for signed integers, which maps signed values
/// to unsigned in a way that preserves small magnitude values for efficient varint encoding.
/// </summary>
internal static class ZigZag
{
    /// <summary>
    /// Encodes a 32-bit signed integer into a 32-bit unsigned integer using ZigZag.
    /// This maps negative values to odd numbers and non-negative values to even numbers.
    /// </summary>
    /// <param name="value">Signed 32-bit integer to encode.</param>
    /// <returns>Unsigned 32-bit ZigZag-encoded result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Encode32(int value)
        => (uint)((value << 1) ^ (value >> 31));

    /// <summary>
    /// Encodes a 64-bit signed integer into a 64-bit unsigned integer using ZigZag.
    /// This maps negative values to odd numbers and non-negative values to even numbers.
    /// </summary>
    /// <param name="value">Signed 64-bit integer to encode.</param>
    /// <returns>Unsigned 64-bit ZigZag-encoded result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Encode64(long value)
        => (ulong)((value << 1) ^ (value >> 63));

    /// <summary>
    /// Decodes a 32-bit unsigned ZigZag-encoded integer back to a signed 32-bit integer.
    /// This reverses the Encode32 operation.
    /// </summary>
    /// <param name="n">Unsigned 32-bit ZigZag-encoded value.</param>
    /// <returns>Decoded signed 32-bit integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Decode32(uint n)
        => (int)(n >> 1) ^ -(int)(n & 1);

    /// <summary>
    /// Decodes a 64-bit unsigned ZigZag-encoded integer back to a signed 64-bit integer.
    /// This reverses the Encode64 operation.
    /// </summary>
    /// <param name="n">Unsigned 64-bit ZigZag-encoded value.</param>
    /// <returns>Decoded signed 64-bit integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Decode64(ulong n)
        => (long)(n >> 1) ^ -(long)(n & 1);
}
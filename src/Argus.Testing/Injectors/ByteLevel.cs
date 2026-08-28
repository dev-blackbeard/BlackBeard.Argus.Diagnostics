using System;

namespace Argus.Testing.Injectors;

/// <summary>
/// The byte-level transformations the wire actually performs.
/// </summary>
/// <remarks>
/// Kept in one place, and kept honest: these operate on the eight bytes of an IEEE 754
/// double, exactly as a mismatched reader would. An injector that instead approximated the
/// fault by multiplying by a factor or adding an offset would be testing the detector
/// against somebody's mental model of corruption rather than against corruption.
/// </remarks>
public static class ByteLevel
{
    /// <summary>Reverses the eight bytes of a double, as an endianness mismatch does.</summary>
    /// <param name="value">The value as written.</param>
    /// <returns>The value as read by the other byte order.</returns>
    public static double SwapEndian(double value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>
    /// Rotates a buffer of doubles by a whole number of bytes, as a framing misalignment does.
    /// </summary>
    /// <param name="values">The fields, in declared order.</param>
    /// <param name="byteShift">
    /// How many bytes to shift by. Positive shifts the reader forward, so each field picks up
    /// bytes belonging to the field after it.
    /// </param>
    /// <returns>The fields as the misaligned reader would decode them.</returns>
    /// <remarks>
    /// A rotation rather than a shift, so no byte is invented. That is the conservative choice
    /// for a test: a real misalignment reads whatever is genuinely adjacent in the frame, which
    /// may be another field, a header, or the next message — and a rotation is the case where
    /// the adjacent bytes are the most plausible ones there could be, so a detector that
    /// catches this catches the easier cases too.
    /// </remarks>
    public static double[] ShiftBytes(double[] values, int byteShift)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        int length = values.Length * sizeof(double);
        if (length == 0)
        {
            return new double[0];
        }

        var buffer = new byte[length];
        for (int i = 0; i < values.Length; i++)
        {
            byte[] bytes = BitConverter.GetBytes(values[i]);
            Array.Copy(bytes, 0, buffer, i * sizeof(double), sizeof(double));
        }

        var shifted = new byte[length];
        for (int i = 0; i < length; i++)
        {
            int source = ((i + byteShift) % length + length) % length;
            shifted[i] = buffer[source];
        }

        var result = new double[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = BitConverter.ToDouble(shifted, i * sizeof(double));
        }

        return result;
    }

    /// <summary>Narrows a double to 32-bit floating point and back.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value after a round trip through <see cref="float"/>.</returns>
    public static double NarrowToSingle(double value)
    {
        return (float)value;
    }
}

namespace Argus.Contracts;

/// <summary>
/// One numeric field as it was decoded from the wire, with the offset it was read from.
/// </summary>
/// <remarks>
/// Supplying these lets encoding detectors reason about the frame as a byte layout rather
/// than as a set of semantic properties. The field-shift inference in particular needs the
/// declared order and offsets: its whole argument is that <i>every</i> field's magnitude
/// matches the field a fixed number of bytes away.
/// </remarks>
public sealed class RawField
{
    /// <summary>Creates a raw field record.</summary>
    /// <param name="name">The field's name in the producing struct.</param>
    /// <param name="byteOffset">The field's byte offset within the frame.</param>
    /// <param name="byteLength">The field's width in bytes.</param>
    /// <param name="value">The decoded numeric value.</param>
    public RawField(string name, int byteOffset, int byteLength, double value)
    {
        Name = name;
        ByteOffset = byteOffset;
        ByteLength = byteLength;
        Value = value;
    }

    /// <summary>The field's name in the producing struct.</summary>
    public string Name { get; }

    /// <summary>The field's byte offset within the frame.</summary>
    public int ByteOffset { get; }

    /// <summary>The field's width in bytes.</summary>
    public int ByteLength { get; }

    /// <summary>The decoded numeric value.</summary>
    public double Value { get; }
}

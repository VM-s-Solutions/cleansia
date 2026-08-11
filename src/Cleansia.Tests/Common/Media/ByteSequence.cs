namespace Cleansia.Tests.Common.Media;

/// <summary>
/// Sub-sequence search over raw bytes, so the scrub's output can be asserted without a second parser
/// standing between the assertion and the file. A reader written for the tests would share the
/// production walker's assumptions and go green on the same mistakes; "these bytes are gone" and "these
/// exact 36 bytes are present" share nothing with it.
/// </summary>
internal static class ByteSequence
{
    public static bool Contains(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle) =>
        IndexOf(haystack, needle) >= 0;

    public static int IndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return -1;
        }

        for (var start = 0; start <= haystack.Length - needle.Length; start++)
        {
            if (haystack.Slice(start, needle.Length).SequenceEqual(needle))
            {
                return start;
            }
        }

        return -1;
    }

    public static int Count(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        var found = 0;
        var offset = 0;

        while (offset <= haystack.Length - needle.Length)
        {
            var next = IndexOf(haystack[offset..], needle);
            if (next < 0)
            {
                break;
            }

            found++;
            offset += next + 1;
        }

        return found;
    }
}

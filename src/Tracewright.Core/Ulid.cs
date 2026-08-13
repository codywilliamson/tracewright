using System.Security.Cryptography;

namespace Tracewright.Core;

/// <summary>
/// ULID: 48-bit ms timestamp + 80 random bits, Crockford base32, 26 chars, uppercase,
/// lexically sortable by creation time. No external package.
/// </summary>
public static class Ulid
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int RandomByteCount = 10;
    private const int TimestampCharCount = 10;
    private const int RandomCharCount = 16;

    public static string NewUlid()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Span<byte> randomBytes = stackalloc byte[RandomByteCount];
        RandomNumberGenerator.Fill(randomBytes);

        Span<char> result = stackalloc char[TimestampCharCount + RandomCharCount];
        EncodeTimestamp(timestamp, result[..TimestampCharCount]);
        EncodeRandomness(randomBytes, result[TimestampCharCount..]);

        return new string(result);
    }

    // 48 bits packed into 10 base32 chars (5 bits each); top 2 bits of the first char are always 0.
    private static void EncodeTimestamp(long timestamp, Span<char> buffer)
    {
        for (var i = 0; i < TimestampCharCount; i++)
        {
            var shift = 45 - i * 5;
            buffer[i] = Alphabet[(int)((timestamp >> shift) & 0x1F)];
        }
    }

    // 80 bits (10 bytes) packed into 16 base32 chars (5 bits each) — divides evenly, no padding.
    private static void EncodeRandomness(ReadOnlySpan<byte> random, Span<char> buffer)
    {
        var bitBuffer = 0;
        var bitCount = 0;
        var outIndex = 0;

        foreach (var b in random)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitCount += 8;

            while (bitCount >= 5)
            {
                bitCount -= 5;
                buffer[outIndex++] = Alphabet[(bitBuffer >> bitCount) & 0x1F];
            }

            bitBuffer &= (1 << bitCount) - 1;
        }
    }
}

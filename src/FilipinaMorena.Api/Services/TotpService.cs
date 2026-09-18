using System.Security.Cryptography;
using System.Text;

namespace FilipinaMorena.Api.Services;

public sealed class TotpService
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Base32Encode(bytes);
    }

    public bool Verify(string secret, string code)
    {
        var sanitized = new string(code.Where(char.IsDigit).ToArray());
        if (sanitized.Length != 6) return false;

        var currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        return Enumerable.Range(-1, 3)
            .Select(offset => GenerateCode(secret, currentStep + offset))
            .Any(expected => CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(sanitized)));
    }

    private static string GenerateCode(string secret, long step)
    {
        var key = Base32Decode(secret);
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static string Base32Encode(byte[] data)
    {
        var output = new StringBuilder();
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                output.Append(Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }
        if (bitsLeft > 0) output.Append(Alphabet[(buffer << (5 - bitsLeft)) & 31]);
        return output.ToString();
    }

    private static byte[] Base32Decode(string value)
    {
        var bytes = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var character in value.TrimEnd('=').ToUpperInvariant())
        {
            var index = Alphabet.IndexOf(character);
            if (index < 0) throw new FormatException("Invalid Base32 value.");
            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft < 8) continue;
            bytes.Add((byte)(buffer >> (bitsLeft - 8)));
            bitsLeft -= 8;
        }
        return bytes.ToArray();
    }
}

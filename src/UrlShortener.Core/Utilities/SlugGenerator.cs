using System.Security.Cryptography;

namespace UrlShortener.Core.Utilities;

// Generates a short, URL-safe random identifier.
// 6 base62 chars = 62^6 ≈ 56 billion combinations. Collisions are rare;
// the service retries once on a unique-constraint violation.
public static class SlugGenerator
{
    private const string Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string Generate(int length = 6)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return new string(chars);
    }
}

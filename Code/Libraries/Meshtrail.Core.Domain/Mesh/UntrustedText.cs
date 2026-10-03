using System.Text;

namespace Meshtrail.Core.Domain.Mesh;

/// <summary>
/// Cleans text that arrives over the radio (names, messages). Anyone with a radio can send anything,
/// so we drop control characters, trim and cut it to a maximum length before storing it.
/// </summary>
public static class UntrustedText
{
    public static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            // Keep line breaks and tabs readable as spaces; drop other control characters (incl. the bell, U+0007).
            if (char.IsControl(character))
            {
                if (character is '\n' or '\r' or '\t')
                {
                    builder.Append(' ');
                }

                continue;
            }

            builder.Append(character);
        }

        var cleaned = builder.ToString().Trim();
        if (cleaned.Length == 0)
        {
            return null;
        }

        if (cleaned.Length <= maxLength)
        {
            return cleaned;
        }

        // Do not cut an emoji (surrogate pair) in half.
        var cut = char.IsHighSurrogate(cleaned[maxLength - 1]) ? maxLength - 1 : maxLength;
        return cleaned[..cut];
    }
}

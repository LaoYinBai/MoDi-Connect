using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MoDi.Desktop.Platform.Logging;

internal static partial class LogRedactor
{
    public static string Fingerprint(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(hash).ToLowerInvariant()[..12];
    }

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var redacted = SecretAssignmentRegex().Replace(
            value,
            match => $"{match.Groups[1].Value}=[REDACTED]");
        redacted = UserPathRegex().Replace(redacted, "[USER_PATH]");
        return Ipv4Regex().Replace(redacted, "[IP]");
    }

    [GeneratedRegex(@"(?i)\b(token|qr)\s*=\s*[^\s,;""']+")]
    private static partial Regex SecretAssignmentRegex();

    [GeneratedRegex(@"(?i)\b[A-Z]:\\Users\\[^\\\s""']+(?:\\[^\s""']*)?")]
    private static partial Regex UserPathRegex();

    [GeneratedRegex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b")]
    private static partial Regex Ipv4Regex();
}

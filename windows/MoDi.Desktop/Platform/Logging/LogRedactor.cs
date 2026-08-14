using System;
using System.Text.RegularExpressions;

namespace MoDi.Desktop.Platform.Logging;

internal static partial class LogRedactor
{
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

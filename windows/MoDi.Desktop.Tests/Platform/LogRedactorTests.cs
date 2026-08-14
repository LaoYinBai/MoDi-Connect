using MoDi.Desktop.Platform.Logging;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class LogRedactorTests
{
    [Theory]
    [InlineData("token=abc123", "token=[REDACTED]")]
    [InlineData("192.168.1.44", "[IP]")]
    [InlineData(@"C:\Users\Alice\Music", "[USER_PATH]")]
    [InlineData("qr=modi://pair/secret", "qr=[REDACTED]")]
    public void Redactor_removes_sensitive_values(string input, string expected) =>
        Assert.Equal(expected, LogRedactor.Redact(input));
}

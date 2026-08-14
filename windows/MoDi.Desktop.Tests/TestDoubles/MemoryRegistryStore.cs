using MoDi.Desktop.Platform.Startup;

namespace MoDi.Desktop.Tests.TestDoubles;

internal sealed class MemoryRegistryStore : IRegistryStore
{
    public string? Value { get; set; }
    public Exception? Exception { get; set; }
    public int ReadCalls { get; private set; }
    public int WriteCalls { get; private set; }
    public int DeleteCalls { get; private set; }
    public string? LastSubKey { get; private set; }
    public string? LastValueName { get; private set; }
    public string? LastWrittenValue { get; private set; }

    public string? ReadCurrentUserString(string subKey, string valueName)
    {
        ThrowIfConfigured();
        ReadCalls++;
        LastSubKey = subKey;
        LastValueName = valueName;
        return Value;
    }

    public void WriteCurrentUserString(string subKey, string valueName, string value)
    {
        ThrowIfConfigured();
        WriteCalls++;
        LastSubKey = subKey;
        LastValueName = valueName;
        LastWrittenValue = value;
        Value = value;
    }

    public void DeleteCurrentUserValue(string subKey, string valueName)
    {
        ThrowIfConfigured();
        DeleteCalls++;
        LastSubKey = subKey;
        LastValueName = valueName;
        Value = null;
    }

    private void ThrowIfConfigured()
    {
        if (Exception is not null)
            throw Exception;
    }
}

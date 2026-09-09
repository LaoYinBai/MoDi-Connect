using System.Text.Json;
using MoDi.App.Contracts.Connectivity;
using MoDi.Desktop.Platform.Logging;
using MoDi.Desktop.Tests.TestDoubles;
using Xunit;

namespace MoDi.Desktop.Tests.Platform;

public sealed class ConnectivityLoggingTests
{
    [Fact]
    public void Context_is_correlatable_without_exposing_complete_identifiers()
    {
        using var temp = TempDirectory.Create();
        using var writer = new StructuredLogService(temp.Path, TimeProvider.System);
        var context = new ConnectivityLogContext(
            PeerId.Parse("private-device-123456"),
            SessionId.Parse("session-secret-987654"),
            ChannelId.Parse("audio/primary"),
            TransportKind.WifiDirect,
            ChannelDirection.Receive,
            42,
            SessionState.Streaming);

        writer.Write("INFO", "connection", "token=not-for-logs", context: context);
        writer.Write("INFO", "connection", "second event", context: context);

        var content = File.ReadAllText(Assert.Single(Directory.GetFiles(temp.Path, "*.jsonl")));
        Assert.DoesNotContain("private-device-123456", content);
        Assert.DoesNotContain("session-secret-987654", content);
        Assert.DoesNotContain("not-for-logs", content);

        var entries = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Equal(entries[0].GetProperty("Context").GetProperty("Peer").GetString(),
            entries[1].GetProperty("Context").GetProperty("Peer").GetString());
        Assert.Equal("audio/primary", entries[0].GetProperty("Context").GetProperty("Channel").GetString());
        Assert.Equal("WifiDirect", entries[0].GetProperty("Context").GetProperty("Transport").GetString());
        Assert.Equal(42, entries[0].GetProperty("Context").GetProperty("Sequence").GetInt64());
    }

    [Fact]
    public void Existing_write_overload_still_emits_an_entry_without_context()
    {
        using var temp = TempDirectory.Create();
        using var writer = new StructuredLogService(temp.Path, TimeProvider.System);
        writer.Write("INFO", "legacy", "still supported");

        using var entry = JsonDocument.Parse(File.ReadAllText(Assert.Single(Directory.GetFiles(temp.Path, "*.jsonl"))));
        Assert.Equal(JsonValueKind.Null, entry.RootElement.GetProperty("Context").ValueKind);
    }

    [Fact]
    public void Concurrent_session_contexts_keep_distinct_safe_correlation_ids()
    {
        using var temp = TempDirectory.Create();
        using var writer = new StructuredLogService(temp.Path, TimeProvider.System);
        writer.Write("INFO", "connection", "session A", context: new ConnectivityLogContext(
            SessionId: SessionId.Parse("phone-a"), ChannelId: AudioChannel.Primary, Sequence: 0));
        writer.Write("INFO", "connection", "session B", context: new ConnectivityLogContext(
            SessionId: SessionId.Parse("phone-b"), ChannelId: AudioChannel.Primary, Sequence: 0));

        var entries = File.ReadAllLines(Assert.Single(Directory.GetFiles(temp.Path, "*.jsonl")))
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToArray();
        var first = entries[0].GetProperty("Context");
        var second = entries[1].GetProperty("Context");
        Assert.NotEqual(first.GetProperty("Session").GetString(), second.GetProperty("Session").GetString());
        Assert.Equal("audio/primary", first.GetProperty("Channel").GetString());
        Assert.Equal("audio/primary", second.GetProperty("Channel").GetString());
        Assert.Equal(0, first.GetProperty("Sequence").GetInt64());
        Assert.Equal(0, second.GetProperty("Sequence").GetInt64());
    }

    [Fact]
    public void Core_adapter_applies_and_then_restores_scoped_context()
    {
        using var temp = TempDirectory.Create();
        using var writer = new StructuredLogService(temp.Path, TimeProvider.System);
        var adapter = new CoreLoggerAdapter(writer);
        using (CoreLoggerAdapter.BeginContext(new ConnectivityLogContext(
            Transport: TransportKind.Lan,
            State: SessionState.Connecting)))
        {
            adapter.Info("connection", "scoped");
        }
        adapter.Info("connection", "legacy");

        var entries = File.ReadAllLines(Assert.Single(Directory.GetFiles(temp.Path, "*.jsonl")))
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToArray();
        Assert.Equal("Lan", entries[0].GetProperty("Context").GetProperty("Transport").GetString());
        Assert.Equal(JsonValueKind.Null, entries[1].GetProperty("Context").ValueKind);
    }
}

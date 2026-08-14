using System.Text.Json;
using System.Text.RegularExpressions;

namespace MoDi.Architecture.Tests;

public sealed class ProtocolMigrationInventoryTests
{
    private const string ApiInventoryPath = "docs/protocol/package-b/current-api-0.1.0.txt";
    private const string SourceInventoryPath = "docs/protocol/package-b/current-source-hashes-0.1.0.json";
    private const string LegalChecklistPath = "docs/protocol/package-b/legal-review-checklist.md";

    [Fact]
    public void Migration_snapshot_preserves_the_pre_boundary_source_inventory()
    {
        var inventoryPath = RepositoryLayout.Resolve(SourceInventoryPath);
        Assert.True(File.Exists(inventoryPath), $"Missing source inventory: {SourceInventoryPath}");

        using var document = JsonDocument.Parse(File.ReadAllBytes(inventoryPath));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("0.1.0", root.GetProperty("protocolVersion").GetString());

        Assert.Equal("cfe8406a2a6201bd3e77df683983dfd7fc91dc59", root.GetProperty("applicationSnapshotCommit").GetString());
        var recorded = root.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal(42, recorded.Length);
        foreach (var item in recorded)
        {
            var path = item.GetProperty("path").GetString()!;
            Assert.Matches(@"^MoDi-Connect-Protocol-(zh|en)/src/(csharp|kotlin)/", path);
            Assert.True(item.GetProperty("length").GetInt64() > 0);
            Assert.Matches("^[0-9a-f]{64}$", item.GetProperty("sha256").GetString()!);
        }

        Assert.False(Directory.Exists(RepositoryLayout.Resolve("MoDi-Connect-Protocol-zh/src")));
        Assert.False(Directory.Exists(RepositoryLayout.Resolve("MoDi-Connect-Protocol-en/src")));
    }

    [Fact]
    public void Api_inventory_freezes_both_public_surfaces_and_wire_constants()
    {
        var inventoryPath = RepositoryLayout.Resolve(ApiInventoryPath);
        Assert.True(File.Exists(inventoryPath), $"Missing API inventory: {ApiInventoryPath}");
        var inventory = File.ReadAllText(inventoryPath);

        string[] requiredSections =
        [
            "[C#] MoDi.Protocol.IPacketProtocol",
            "[C#] MoDi.Protocol.ITransport",
            "[C#] MoDi.Protocol.LinkType",
            "[C#] MoDi.Protocol.Packet",
            "[C#] MoDi.Protocol.PacketHeader",
            "[C#] MoDi.Protocol.PacketHeader.DecodedHeader",
            "[C#] MoDi.Protocol.PacketHeaderCodec",
            "[C#] MoDi.Protocol.PacketType",
            "[C#] MoDi.Protocol.SequenceHelper",
            "[C#] MoDi.Protocol.StreamFrameDecoder",
            "[C#] MoDi.Protocol.TransportType",
            "[Kotlin] com.modi.protocol.IPacketProtocol",
            "[Kotlin] com.modi.protocol.ITransport",
            "[Kotlin] com.modi.protocol.LinkType",
            "[Kotlin] com.modi.protocol.Packet",
            "[Kotlin] com.modi.protocol.PacketHeader",
            "[Kotlin] com.modi.protocol.PacketHeaderInfo",
            "[Kotlin] com.modi.protocol.PacketHeaderCodec",
            "[Kotlin] com.modi.protocol.PacketType",
            "[Kotlin] com.modi.protocol.SequenceHelper",
            "[Kotlin] com.modi.protocol.StreamFrameDecoder",
            "[Kotlin] com.modi.protocol.TransportType",
        ];

        foreach (var section in requiredSections)
            Assert.Contains(section, inventory, StringComparison.Ordinal);

        Assert.Contains("Magic = 0x4C414242", inventory, StringComparison.Ordinal);
        Assert.Contains("Version = 0x02", inventory, StringComparison.Ordinal);
        Assert.Contains("HeaderSize = 15", inventory, StringComparison.Ordinal);
        Assert.Contains("WifiLan = 0x01", inventory, StringComparison.Ordinal);
        Assert.Contains("WifiDirect = 0x02", inventory, StringComparison.Ordinal);
        Assert.Contains("Bluetooth = 0x03", inventory, StringComparison.Ordinal);
        Assert.Contains("Usb = 0x04", inventory, StringComparison.Ordinal);
    }

    [Fact]
    public void Legal_checklist_keeps_every_distribution_gate_unapproved()
    {
        var checklistPath = RepositoryLayout.Resolve(LegalChecklistPath);
        Assert.True(File.Exists(checklistPath), $"Missing legal checklist: {LegalChecklistPath}");
        var checklist = File.ReadAllText(checklistPath);

        Assert.Contains("PACKAGE_B_DISTRIBUTION_GATE: BLOCKED", checklist, StringComparison.Ordinal);
        string[] requiredGates =
        [
            "代码著作权与第三方贡献",
            "既往公开与交付历史",
            "协议专有许可证",
            "二进制再分发授权",
            "GPLv3 第 7 节链接例外",
            "第三方依赖与声明",
            "出口、隐私与加密审查",
            "合格法律审核签字",
        ];

        var rows = Regex.Matches(checklist, @"^\|\s*(?<gate>[^|]+?)\s*\|\s*(?<status>[^|]+?)\s*\|\s*(?<reviewer>[^|]*?)\s*\|\s*(?<date>[^|]*?)\s*\|\r?$", RegexOptions.Multiline)
            .Select(match => new
            {
                Gate = match.Groups["gate"].Value.Trim(),
                Status = match.Groups["status"].Value.Trim(),
                Reviewer = match.Groups["reviewer"].Value.Trim(),
                Date = match.Groups["date"].Value.Trim(),
            })
            .Where(row => requiredGates.Contains(row.Gate, StringComparer.Ordinal))
            .ToDictionary(row => row.Gate, StringComparer.Ordinal);

        Assert.Equal(requiredGates.Order(StringComparer.Ordinal), rows.Keys.Order(StringComparer.Ordinal));
        foreach (var gate in requiredGates)
        {
            Assert.Equal("未批准", rows[gate].Status);
            Assert.Equal("—", rows[gate].Reviewer);
            Assert.Equal("—", rows[gate].Date);
        }
    }
}

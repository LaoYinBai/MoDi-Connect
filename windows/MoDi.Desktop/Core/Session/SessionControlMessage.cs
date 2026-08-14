using System;
using System.Text;
using MoDi.Protocol;

namespace MoDi.Desktop.Core.Session;

public enum SessionControlAction : byte
{
    DisconnectRequest = 1,
    DisconnectAck = 2,
}

public enum DisconnectReason : byte
{
    UserSwitch = 1,
    Repair = 2,
    UserStop = 3,
}

public enum DisconnectResult : byte
{
    None = 0,
    Accepted = 1,
    Ignored = 2,
}

public readonly record struct SessionControlMessage(
    SessionControlAction Action,
    Guid SessionId,
    byte OldLink,
    byte TargetLink,
    DisconnectReason Reason,
    DisconnectResult Result)
{
    private const int MessageLength = 24;
    private const byte Domain = 0x0F;
    private const byte MessageType = 0x01;
    private const byte Version = 0x01;

    public static SessionControlMessage Request(
        Guid sessionId,
        byte oldLink,
        byte targetLink,
        DisconnectReason reason)
    {
        if (sessionId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(sessionId));
        if (!IsLinkType(oldLink)) throw new ArgumentOutOfRangeException(nameof(oldLink));
        if (!IsLinkType(targetLink)) throw new ArgumentOutOfRangeException(nameof(targetLink));
        return new(SessionControlAction.DisconnectRequest, sessionId, oldLink, targetLink, reason, DisconnectResult.None);
    }

    public static SessionControlMessage Ack(SessionControlMessage request, DisconnectResult result)
    {
        if (request.Action != SessionControlAction.DisconnectRequest)
            throw new ArgumentException("ACK requires a disconnect request.", nameof(request));
        if (result == DisconnectResult.None)
            throw new ArgumentOutOfRangeException(nameof(result), "ACK result must be accepted or ignored.");
        return request with { Action = SessionControlAction.DisconnectAck, Result = result };
    }

    public byte[] Encode()
    {
        var payload = new byte[MessageLength];
        payload[0] = Domain;
        payload[1] = MessageType;
        payload[2] = Version;
        payload[3] = (byte)Action;
        WriteGuid(SessionId, payload.AsSpan(4, 16));
        payload[20] = OldLink;
        payload[21] = TargetLink;
        payload[22] = (byte)Reason;
        payload[23] = (byte)Result;
        return payload;
    }

    public Packet ToPacket() => new()
    {
        Type = PacketType.Data,
        LinkType = OldLink,
        Sequence = 0,
        Payload = Encode(),
    };

    public static bool TryDecode(ReadOnlySpan<byte> payload, out SessionControlMessage message)
    {
        message = default;
        if (payload.Length != MessageLength || payload[0] != Domain || payload[1] != MessageType || payload[2] != Version)
            return false;
        if (!Enum.IsDefined((SessionControlAction)payload[3]) ||
            !IsLinkType(payload[20]) || !IsLinkType(payload[21]) ||
            !Enum.IsDefined((DisconnectReason)payload[22]) ||
            !Enum.IsDefined((DisconnectResult)payload[23]))
            return false;

        var action = (SessionControlAction)payload[3];
        var result = (DisconnectResult)payload[23];
        if (action == SessionControlAction.DisconnectRequest && result != DisconnectResult.None)
            return false;
        if (action == SessionControlAction.DisconnectAck && result == DisconnectResult.None)
            return false;

        message = new(
            action,
            ReadGuid(payload.Slice(4, 16)),
            payload[20],
            payload[21],
            (DisconnectReason)payload[22],
            result);
        return true;
    }

    private static bool IsLinkType(byte linkType) => linkType is
        LinkType.WifiLan or LinkType.WifiDirect or LinkType.Bluetooth or LinkType.Usb;

    internal static void WriteGuid(Guid value, Span<byte> destination) =>
        Convert.FromHexString(value.ToString("N")).CopyTo(destination);

    internal static Guid ReadGuid(ReadOnlySpan<byte> source) =>
        Guid.ParseExact(Convert.ToHexString(source), "N");
}

public readonly record struct HelloSessionIdentity(int Route, string? Token, Guid SessionId);

public readonly record struct HandshakeResult(int Route, Guid SessionId);

public static class HelloSessionPayload
{
    private const int TokenLength = 8;
    private const int SessionLength = 16;

    public static byte[] Encode(int route, string? token, Guid sessionId)
    {
        if (route is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(route));
        if (sessionId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(sessionId));
        byte[]? tokenBytes = token is null ? null : Encoding.ASCII.GetBytes(token);
        if (tokenBytes is { Length: < 1 or > TokenLength } ||
            token is not null && Encoding.ASCII.GetString(tokenBytes!) != token)
            throw new ArgumentException("Token must contain 1 to 8 ASCII bytes.", nameof(token));

        var sessionOffset = 1 + (tokenBytes is null ? 0 : TokenLength);
        var payload = new byte[sessionOffset + SessionLength];
        payload[0] = (byte)route;
        tokenBytes?.CopyTo(payload, 1);
        SessionControlMessage.WriteGuid(sessionId, payload.AsSpan(sessionOffset, SessionLength));
        return payload;
    }

    public static bool TryDecode(
        ReadOnlySpan<byte> payload,
        bool tokenRequired,
        out HelloSessionIdentity identity)
    {
        identity = default;
        var sessionOffset = tokenRequired ? 1 + TokenLength : 1;
        if (payload.Length != sessionOffset + SessionLength || payload[0] > 3)
            return false;

        string? token = null;
        if (tokenRequired)
        {
            var tokenBytes = payload.Slice(1, TokenLength);
            var terminator = tokenBytes.IndexOf((byte)0);
            var tokenLength = terminator < 0 ? TokenLength : terminator;
            if (tokenLength == 0 ||
                terminator >= 0 && tokenBytes.Slice(terminator).IndexOfAnyExcept((byte)0) >= 0 ||
                !AllPrintableAscii(tokenBytes.Slice(0, tokenLength)))
                return false;
            token = Encoding.ASCII.GetString(tokenBytes.Slice(0, tokenLength));
        }

        identity = new(
            payload[0],
            token,
            SessionControlMessage.ReadGuid(payload.Slice(sessionOffset, SessionLength)));
        return true;
    }

    public static bool MatchesAck(ReadOnlySpan<byte> payload, Guid expectedSessionId) =>
        TryDecode(payload, tokenRequired: false, out var identity) && identity.SessionId == expectedSessionId;

    private static bool AllPrintableAscii(ReadOnlySpan<byte> value)
    {
        foreach (var current in value)
            if (current is < 0x20 or > 0x7E)
                return false;
        return true;
    }
}

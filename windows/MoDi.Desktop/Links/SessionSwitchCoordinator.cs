using System;
using System.Collections.Generic;
using MoDi.Desktop.Core.Session;
using MoDi.Protocol;

namespace MoDi.Desktop.Links;

internal readonly record struct ActiveSession(byte LinkType, Guid SessionId, long Generation);

internal readonly record struct SessionDecision(
    bool Accepted,
    ActiveSession? Ended,
    SessionControlMessage Ack);

internal sealed class SessionSwitchCoordinator
{
    private readonly object _gate = new();
    private readonly HashSet<(byte LinkType, Guid SessionId)> _acceptedDisconnects = [];
    private long _generation;

    public ActiveSession? Current { get; private set; }

    public ActiveSession Activate(byte linkType, Guid sessionId)
    {
        if (!IsLinkType(linkType)) throw new ArgumentOutOfRangeException(nameof(linkType));
        if (sessionId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(sessionId));

        lock (_gate)
        {
            var activated = new ActiveSession(linkType, sessionId, checked(++_generation));
            Current = activated;
            return activated;
        }
    }

    public SessionDecision HandleDisconnect(SessionControlMessage request, byte receivedOnLink)
    {
        if (request.Action != SessionControlAction.DisconnectRequest)
            throw new ArgumentException("Only disconnect requests can be handled.", nameof(request));

        lock (_gate)
        {
            var key = (request.OldLink, request.SessionId);
            if (receivedOnLink == request.OldLink && _acceptedDisconnects.Contains(key))
                return AcceptedDecision(request, ended: null);

            if (receivedOnLink != request.OldLink ||
                Current is not { } current ||
                current.LinkType != request.OldLink ||
                current.SessionId != request.SessionId)
            {
                return new SessionDecision(
                    Accepted: false,
                    Ended: null,
                    Ack: SessionControlMessage.Ack(request, DisconnectResult.Ignored));
            }

            Current = null;
            _acceptedDisconnects.Add(key);
            return AcceptedDecision(request, current);
        }
    }

    public bool EndIfCurrent(byte linkType, Guid sessionId)
    {
        lock (_gate)
        {
            if (Current is not { } current ||
                current.LinkType != linkType || current.SessionId != sessionId)
                return false;

            Current = null;
            return true;
        }
    }

    private static SessionDecision AcceptedDecision(
        SessionControlMessage request,
        ActiveSession? ended) => new(
            Accepted: true,
            Ended: ended,
            Ack: SessionControlMessage.Ack(request, DisconnectResult.Accepted));

    private static bool IsLinkType(byte linkType) => linkType is
        LinkType.WifiLan or LinkType.WifiDirect or LinkType.Bluetooth or LinkType.Usb;
}

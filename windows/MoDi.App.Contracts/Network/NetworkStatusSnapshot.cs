namespace MoDi.App.Contracts;

public sealed record NetworkStatusSnapshot(
    string CurrentLinkLabel,
    string LocalIpAddress,
    int AudioPort,
    int HandshakePort,
    IReadOnlyList<LinkStatusSnapshot> Links);

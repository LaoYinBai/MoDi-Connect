namespace MoDi.App.Contracts;

public enum ReceiverState
{
    Idle,
    Searching,
    Found,
    Connecting,
    Connected,
    Streaming,
    Reconnecting,
    Error,
}

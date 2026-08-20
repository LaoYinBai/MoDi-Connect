using System;

namespace MoDi.Desktop.Network;

public interface INetworkChangeSource : IDisposable
{
    event EventHandler? Changed;
}

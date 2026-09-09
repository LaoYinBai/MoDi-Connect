using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MoDi.Desktop.Services;

namespace MoDi.Desktop.Connectivity.Receiver;

internal interface IReceiverLinkRuntime : IDisposable
{
    event Action<ConnectionState>? ConnectionStateChanged;
    event Action<string>? ActiveLinkChanged;
    event Action<int>? RouteChanged;
    event Action<string, string>? LinkStatusChanged;
    event Action<bool>? P2pProgressVisibleChanged;
    event Action<bool, double>? P2pProgressChanged;
    event Action<string?, string?>? QrChanged;
    event Action<IReadOnlyList<P2pCandidateInfo>>? P2pCandidatesChanged;

    double Volume { get; set; }
    Task<bool> StartLanAsync();
    Task<bool> StartP2pAsync();
    Task StopP2pAsync();
    Task<bool> StartBluetoothAsync();
    Task<bool> StartUsbAsync();
    bool ConnectP2pCandidate(string deviceId);
}

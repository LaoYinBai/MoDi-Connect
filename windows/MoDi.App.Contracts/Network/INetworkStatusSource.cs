namespace MoDi.App.Contracts;

public interface INetworkStatusSource : IStateSource<NetworkStatusSnapshot>, IDisposable
{
}

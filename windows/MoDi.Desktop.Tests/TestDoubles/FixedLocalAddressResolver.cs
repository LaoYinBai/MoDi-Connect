using MoDi.Desktop.Adapters;

namespace MoDi.Desktop.Tests.TestDoubles;

internal sealed class FixedLocalAddressResolver(string? address) : ILocalAddressResolver
{
    public string? GetPreferredIpv4() => address;
}

using MoDi.Desktop.Core.Session;
using MoDi.Desktop.Links;
using Xunit;

namespace MoDi.Desktop.Tests.Links;

public sealed class UsbHandshakeLifetimeTests
{
    [Fact]
    public async Task Closed_proxy_connection_does_not_wait_for_the_hello_deadline()
    {
        var pending=new TaskCompletionSource<HandshakeResult?>();
        var result=await UsbPassiveHandshake.WaitUntilCompletedOrDisconnectedAsync(pending.Task,()=>false,CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Null(result);
    }

    [Fact]
    public async Task Shutdown_cancels_waiting_instead_of_restarting_the_server()
    {
        var pending=new TaskCompletionSource<HandshakeResult?>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UsbPassiveHandshake.WaitUntilCompletedOrDisconnectedAsync(pending.Task,()=>true,new CancellationToken(true)));
    }

    [Fact]
    public async Task Completed_hello_preserves_its_session_identity()
    {
        var identity=new HandshakeResult(2,Guid.NewGuid());
        Assert.Equal(identity,await UsbPassiveHandshake.WaitUntilCompletedOrDisconnectedAsync(Task.FromResult<HandshakeResult?>(identity),()=>true,CancellationToken.None));
    }
}

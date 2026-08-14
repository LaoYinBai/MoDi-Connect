using MoDi.Presentation.Settings;
using MoDi.Presentation.Tests.TestDoubles;

namespace MoDi.Presentation.Tests.Settings;

public sealed class PersonalizationResetCardViewModelTests
{
    [Fact]
    public async Task Reset_does_not_touch_non_personalization_services()
    {
        var reset = new RecordingPersonalizationResetService();
        using var vm = new PersonalizationResetCardViewModel(reset);

        await vm.ConfirmResetCommand.ExecuteAsync();

        Assert.Equal(1, reset.ResetCalls);
        Assert.Equal("个性化设置已重置", vm.FeedbackText);
        Assert.Single(typeof(PersonalizationResetCardViewModel).GetConstructors());
        Assert.Equal(typeof(MoDi.App.Contracts.IPersonalizationResetService),
            Assert.Single(typeof(PersonalizationResetCardViewModel).GetConstructors()).GetParameters().Single().ParameterType);
    }
}

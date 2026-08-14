using System;
using System.Threading;
using System.Threading.Tasks;
using MoDi.App.Contracts;

namespace MoDi.Desktop.Platform.Appearance;

public sealed class PersonalizationResetService : IPersonalizationResetService
{
    private readonly IAppearanceResetTarget _appearance;

    internal PersonalizationResetService(IAppearanceResetTarget appearance) =>
        _appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));

    public Task<OperationResult> ResetAsync(CancellationToken cancellationToken) =>
        _appearance.ResetToDefaultsAsync(cancellationToken);
}

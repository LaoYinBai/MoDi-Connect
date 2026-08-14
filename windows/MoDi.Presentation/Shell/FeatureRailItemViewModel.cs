using MoDi.App.Contracts;
using MoDi.Presentation.Infrastructure;

namespace MoDi.Presentation.Shell;

public sealed class FeatureRailItemViewModel
{
    private readonly IBuiltInFeature _feature;

    public FeatureRailItemViewModel(IBuiltInFeature feature)
    {
        _feature = feature;
        Id = feature.Descriptor.Id;
        DisplayName = feature.Descriptor.DisplayName;
        Description = feature.Descriptor.Description;
        IconKey = feature.Descriptor.IconKey;
        ActivateCommand = new AsyncRelayCommand(feature.ActivateAsync);
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string IconKey { get; }
    public bool IsBuiltIn => true;
    public AsyncRelayCommand ActivateCommand { get; }
}

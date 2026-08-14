namespace MoDi.App.Contracts;

public sealed record BuiltInFeatureDescriptor(
    string Id,
    string DisplayName,
    string Description,
    string IconKey);

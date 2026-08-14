namespace MoDi.App.Contracts;

public interface IBuiltInFeature
{
    BuiltInFeatureDescriptor Descriptor { get; }
    Task<OperationResult> ActivateAsync(CancellationToken cancellationToken);
}

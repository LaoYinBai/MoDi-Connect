namespace MoDi.App.Contracts;

public sealed record LinkStatusSnapshot(
    LinkKind Kind,
    LinkAvailability State,
    string Label,
    string Detail);

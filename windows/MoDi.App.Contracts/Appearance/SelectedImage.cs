namespace MoDi.App.Contracts;

public sealed record SelectedImage(
    string DisplayName,
    ReadOnlyMemory<byte> PngOrJpegBytes);

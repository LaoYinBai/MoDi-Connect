using Avalonia.Media.Imaging;

namespace MoDi.Presentation.P2p;

public static class QrBitmapFactory
{
    public static Bitmap? FromPng(ReadOnlyMemory<byte> pngBytes)
    {
        if (pngBytes.IsEmpty)
            return null;

        try
        {
            using var stream = new MemoryStream(pngBytes.ToArray(), writable: false);
            return new Bitmap(stream);
        }
        catch (Exception) when (pngBytes.Length > 0)
        {
            return null;
        }
    }
}

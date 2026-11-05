// Verilen adresten PNG QR üretir. PngByteQRCode System.Drawing istemez.

using QRCoder;

namespace MiniDpp.Api.Services;

public static class QrCodeGenerator
{
    public static byte[] Png(string metin)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(metin, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(8, drawQuietZones: true);
    }
}

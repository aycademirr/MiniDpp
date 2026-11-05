// QR PNG imzası ve tüketici adresinin rol içermemesi.

using MiniDpp.Api.Services;

namespace MiniDpp.Tests;

public class DppQrTests
{
    private const string Gtin = "08690000000012";

    [Fact]
    public void Qr_ciktisi_gecerli_png()
    {
        var png = QrCodeGenerator.Png($"http://localhost:5118/dpp/{Gtin}");

        Assert.True(png.Length > 8);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png.Take(8).ToArray());
    }

    [Fact]
    public void Adres_rolsuzdur()
    {
        var url = DppUrlBuilder.Build(null, "http", "localhost:5118", null, Gtin);

        Assert.Equal($"http://localhost:5118/dpp/{Gtin}", url);
        Assert.DoesNotContain("?rol", url);
    }

    [Theory]
    [InlineData("https://ornek.com/", "http", "localhost:5118", "/uygulama", "https://ornek.com/dpp/08690000000012")]
    [InlineData("https://ornek.com", "http", "localhost:5118", null, "https://ornek.com/dpp/08690000000012")]
    [InlineData("", "http", "localhost:5118", "/uygulama/", "http://localhost:5118/uygulama/dpp/08690000000012")]
    [InlineData("   ", "https", "dpp.local", null, "https://dpp.local/dpp/08690000000012")]
    public void PublicBaseUrl_doluysa_onu_bosssa_istegi_kullanir(
        string? publicBaseUrl,
        string scheme,
        string host,
        string? pathBase,
        string beklenen)
    {
        var url = DppUrlBuilder.Build(publicBaseUrl, scheme, host, pathBase, Gtin);

        Assert.Equal(beklenen, url);
        Assert.DoesNotContain("//dpp/", url);
        Assert.DoesNotContain("?rol", url);
    }
}

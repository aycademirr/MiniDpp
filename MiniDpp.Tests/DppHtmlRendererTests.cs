// Pasaport HTML'i: metin kodlanır, karbon değeri yazılmaz.

using MiniDpp.Api.Models;
using MiniDpp.Api.Services;

namespace MiniDpp.Tests;

public class DppHtmlRendererTests
{
    [Fact]
    public void Model_adindaki_html_kodlanir()
    {
        var html = DppHtmlRenderer.Render(DppView.From(Ornek("<b>x</b>", 12.4m), false));

        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>x</b>", html);
        Assert.Contains("lang=\"tr\"", html);
    }

    [Fact]
    public void Karbon_degeri_sayfada_yoktur()
    {
        var html = DppHtmlRenderer.Render(DppView.From(Ornek("Slim Denim 501", 12.4m), false));

        Assert.Contains("Slim Denim 501", html);
        Assert.Contains("08690000000012", html);
        Assert.Contains("98%", html);
        Assert.Contains("2%", html);
        Assert.DoesNotContain("12.4", html);
        Assert.DoesNotContain("12,4", html);
        Assert.DoesNotContain("karbon", html, StringComparison.OrdinalIgnoreCase);
    }

    private static Product Ornek(string modelAdi, decimal karbon) =>
        new()
        {
            Gtin = "08690000000012",
            CottonPercent = 98,
            ElastanPercent = 2,
            CarbonKgCo2e = karbon,
            Batch = new Batch
            {
                BatchNo = "B2027-01",
                ProductionPlace = "Denizli",
                Model = new ProductModel { Name = modelAdi }
            }
        };
}

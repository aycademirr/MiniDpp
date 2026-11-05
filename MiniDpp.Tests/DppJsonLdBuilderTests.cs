// JSON-LD: geçerli Product belgesi, karbon yok, HTML ile aynı alanlar.

using System.Reflection;
using System.Text.Json;
using MiniDpp.Api.Controllers;
using MiniDpp.Api.Models;
using MiniDpp.Api.Services;

namespace MiniDpp.Tests;

public class DppJsonLdBuilderTests
{
    private const string Url = "http://localhost:5118/dpp/08690000000012";

    [Fact]
    public void Gecerli_product_belgesi_ve_gtin_metindir()
    {
        using var json = JsonDocument.Parse(Belge(12.4m));
        var kok = json.RootElement;

        Assert.Equal("https://schema.org", kok.GetProperty("@context").GetString());
        Assert.Equal("Product", kok.GetProperty("@type").GetString());
        Assert.Equal(JsonValueKind.String, kok.GetProperty("gtin14").ValueKind);
        Assert.Equal("08690000000012", kok.GetProperty("gtin14").GetString());
        Assert.Equal(Url, kok.GetProperty("url").GetString());
        Assert.Equal("98% pamuk, 2% elastan", kok.GetProperty("material").GetString());
    }

    [Fact]
    public void Karbon_degeri_jsona_yazilmaz()
    {
        var metin = Belge(12.4m);

        Assert.DoesNotContain("12.4", metin);
        Assert.DoesNotContain("12,4", metin);
        Assert.DoesNotContain("karbon", metin, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Html_ve_jsonld_ayni_alanlari_tasir()
    {
        var view = DppView.From(Ornek("Slim Denim 501", 12.4m, 97.5m, 2.5m), false);
        var html = DppHtmlRenderer.Render(view);
        using var json = JsonDocument.Parse(DppJsonLdBuilder.Build(view, Url));
        var kok = json.RootElement;

        Assert.Equal(view.ModelAdi, kok.GetProperty("name").GetString());
        Assert.Contains(view.ModelAdi, html);
        Assert.Contains(view.PamukMetni, html);
        Assert.Contains(view.ElastanMetni, html);
        Assert.Contains(view.Malzeme, kok.GetProperty("material").GetString());

        var ozellikler = kok.GetProperty("additionalProperty").EnumerateArray().ToList();
        Assert.Equal(view.BatchNo, Deger(ozellikler, "Parti numarası").GetString());
        Assert.Equal(view.UretimYeri, Deger(ozellikler, "Üretim yeri").GetString());
        Assert.Equal(view.Pamuk, Deger(ozellikler, "Pamuk").GetDecimal());
        Assert.Equal(view.Elastan, Deger(ozellikler, "Elastan").GetDecimal());
        Assert.Equal("%", Birim(ozellikler, "Pamuk"));
        Assert.Equal("%", Birim(ozellikler, "Elastan"));
        Assert.Contains(view.BatchNo, html);
        Assert.Contains(view.UretimYeri, html);
    }

    [Fact]
    public void Jsonld_rotasi_html_rotasindan_ayri()
    {
        var rotalar = typeof(DppController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributesData())
            .Where(attribute => attribute.AttributeType.Name == "HttpGetAttribute")
            .Select(attribute => attribute.ConstructorArguments[0].Value?.ToString())
            .ToList();

        Assert.Contains("/dpp/{gtin:regex(^\\d{{14}}$)}", rotalar);
        Assert.Contains("/dpp/{gtin:regex(^\\d{{14}}$)}.jsonld", rotalar);
        Assert.Contains("/dpp/{gtin:regex(^\\d{{14}}$)}.png", rotalar);
        Assert.Contains("/api/products", rotalar);
        Assert.Contains("/api/products/{gtin}/versions", rotalar);
        Assert.Equal(5, rotalar.Distinct().Count());
    }

    private static string Belge(decimal karbon) =>
        DppJsonLdBuilder.Build(DppView.From(Ornek("Slim Denim 501", karbon, 98m, 2m), false), Url);

    private static JsonElement Deger(List<JsonElement> ozellikler, string ad) =>
        ozellikler.Single(item => item.GetProperty("name").GetString() == ad).GetProperty("value");

    private static string? Birim(List<JsonElement> ozellikler, string ad) =>
        ozellikler.Single(item => item.GetProperty("name").GetString() == ad).GetProperty("unitText").GetString();

    private static Product Ornek(string modelAdi, decimal karbon, decimal pamuk, decimal elastan) =>
        new()
        {
            Gtin = "08690000000012",
            CottonPercent = pamuk,
            ElastanPercent = elastan,
            CarbonKgCo2e = karbon,
            Batch = new Batch
            {
                BatchNo = "B2027-01",
                ProductionPlace = "Denizli",
                Model = new ProductModel { Name = modelAdi }
            }
        };
}

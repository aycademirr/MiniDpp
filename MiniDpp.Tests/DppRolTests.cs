// Rol: karbon yalnızca ?rol=yetkili iken HTML ve JSON-LD çıktısına girer.

using System.Reflection;
using System.Text.Json;
using MiniDpp.Api.Controllers;
using MiniDpp.Api.Models;
using MiniDpp.Api.Services;

namespace MiniDpp.Tests;

public class DppRolTests
{
    private const string Url = "http://localhost:5118/dpp/08690000000012";

    [Fact]
    public void Rolsuz_ciktilarda_karbon_yoktur()
    {
        var (html, json) = Ciktilar(false);

        Assert.DoesNotContain("12.4", html);
        Assert.DoesNotContain("karbon", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("12.4", json);
        Assert.DoesNotContain("karbon", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Yetkili_rolde_karbon_ikisinde_de_ayni_degerdedir()
    {
        var (html, json) = Ciktilar(true);
        using var belge = JsonDocument.Parse(json);
        var karbon = belge.RootElement.GetProperty("additionalProperty").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "Karbon")
            .GetProperty("value")
            .GetDecimal();

        Assert.Equal(12.4m, karbon);
        Assert.Contains("Karbon (kg CO2e)", html);
        Assert.Contains(karbon.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), html);
        Assert.Equal("kg CO2e", belge.RootElement.GetProperty("additionalProperty").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "Karbon")
            .GetProperty("unitText").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("admin")]
    public void Admin_ve_bos_rol_yetkisizdir(string? rol)
    {
        Assert.False(YetkiliMi(rol));
        var (html, json) = Ciktilar(YetkiliMi(rol));
        Assert.DoesNotContain("12.4", html);
        Assert.DoesNotContain("12.4", json);
    }

    [Fact]
    public void Buyuk_harf_yetkili_karbon_gosterir()
    {
        Assert.True(YetkiliMi("YETKILI"));
        var (html, json) = Ciktilar(YetkiliMi("YETKILI"));
        Assert.Contains("12.4", html);
        Assert.Contains("12.4", json);
    }

    private static (string Html, string Json) Ciktilar(bool yetkili)
    {
        var view = DppView.From(Ornek(), yetkili);
        return (DppHtmlRenderer.Render(view), DppJsonLdBuilder.Build(view, Url));
    }

    private static bool YetkiliMi(string? rol)
    {
        var method = typeof(DppController).GetMethod(
            "YetkiliMi",
            BindingFlags.NonPublic | BindingFlags.Static);
        return (bool)method!.Invoke(null, [rol])!;
    }

    private static Product Ornek() =>
        new()
        {
            Gtin = "08690000000012",
            CottonPercent = 98,
            ElastanPercent = 2,
            CarbonKgCo2e = 12.4m,
            Batch = new Batch
            {
                BatchNo = "B2027-01",
                ProductionPlace = "Denizli",
                Model = new ProductModel { Name = "Slim Denim 501" }
            }
        };
}

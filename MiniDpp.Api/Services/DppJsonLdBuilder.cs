// DppView'dan schema.org Product JSON-LD üretir. Karbon null ise belgeye yazılmaz.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiniDpp.Api.Services;

public static class DppJsonLdBuilder
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Build(DppView view, string url)
    {
        var ozellikler = new JsonArray
        {
            Metin("Parti numarası", view.BatchNo),
            Metin("Üretim yeri", view.UretimYeri),
            Sayi("Pamuk", view.Pamuk),
            Sayi("Elastan", view.Elastan)
        };

        if (view.Karbon is decimal karbon)
            ozellikler.Add(Sayi("Karbon", karbon, "kg CO2e"));

        var belge = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = view.ModelAdi,
            ["gtin14"] = view.Gtin,
            ["url"] = url,
            ["material"] = view.Malzeme,
            ["additionalProperty"] = ozellikler
        };

        return belge.ToJsonString(Json);
    }

    private static JsonObject Metin(string ad, string deger) =>
        new()
        {
            ["@type"] = "PropertyValue",
            ["name"] = ad,
            ["value"] = deger
        };

    private static JsonObject Sayi(string ad, decimal deger, string? birim = "%")
    {
        var ozellik = new JsonObject
        {
            ["@type"] = "PropertyValue",
            ["name"] = ad,
            ["value"] = JsonValue.Create(deger)
        };

        if (birim is not null)
            ozellik["unitText"] = birim;

        return ozellik;
    }
}

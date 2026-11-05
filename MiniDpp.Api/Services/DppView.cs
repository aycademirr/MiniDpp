// Pasaport verisinin tek kaynağı. Karbon yalnızca yetkili rolde doldurulur; HTML ve JSON-LD buradan okur.

using System.Globalization;
using MiniDpp.Api.Models;

namespace MiniDpp.Api.Services;

public sealed record DppView(
    string Gtin,
    string ModelAdi,
    string BatchNo,
    string UretimYeri,
    decimal Pamuk,
    decimal Elastan,
    decimal? Karbon)
{
    public static DppView From(Product product, bool yetkili) =>
        new(
            product.Gtin,
            product.Batch.Model.Name,
            product.Batch.BatchNo,
            product.Batch.ProductionPlace,
            product.CottonPercent,
            product.ElastanPercent,
            yetkili ? product.CarbonKgCo2e : null);

    public string PamukMetni => Yuzde(Pamuk);

    public string ElastanMetni => Yuzde(Elastan);

    public string Malzeme => $"{PamukMetni} pamuk, {ElastanMetni} elastan";

    private static string Yuzde(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture) + "%";
}

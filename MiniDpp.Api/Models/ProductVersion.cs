// Bir ürünün eski sürümü. Güncel kayıt Product tablosunda kalır.

namespace MiniDpp.Api.Models;

public class ProductVersion
{
    public int Id { get; set; }

    public required string Gtin { get; set; }

    public int Version { get; set; }

    public required string ModelAdi { get; set; }

    public required string BatchNo { get; set; }

    public required string UretimYeri { get; set; }

    public decimal CottonPercent { get; set; }

    public decimal ElastanPercent { get; set; }

    public decimal CarbonKgCo2e { get; set; }

    public DateTime UpdatedAt { get; set; }
}

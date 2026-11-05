// Tekil ürün; GTIN birincil anahtardır. Version 1'den başlar, her değişiklikte artar.

namespace MiniDpp.Api.Models;

public class Product
{
    public required string Gtin { get; set; }

    public decimal CottonPercent { get; set; }

    public decimal ElastanPercent { get; set; }

    public decimal CarbonKgCo2e { get; set; }

    public int Version { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int BatchId { get; set; }

    public Batch Batch { get; set; } = null!;
}

// Ürün pasaportu, JSON-LD, QR, ürün listesi ve sürüm geçmişi. ?rol=yetkili yalnızca karbonu açar.

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniDpp.Api.Data;
using MiniDpp.Api.Models;
using MiniDpp.Api.Services;

namespace MiniDpp.Api.Controllers;

[ApiController]
public sealed class DppController : ControllerBase
{
    private readonly DppDbContext _db;
    private readonly IConfiguration _configuration;

    public DppController(DppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet("/dpp/{gtin:regex(^\\d{{14}}$)}.jsonld")]
    public async Task<IActionResult> GetJsonLd(
        string gtin,
        [FromQuery] string? rol,
        CancellationToken cancellationToken)
    {
        var product = await BulAsync(gtin, cancellationToken);
        if (product is null)
            return NotFound();

        var view = DppView.From(product, YetkiliMi(rol));
        return Content(DppJsonLdBuilder.Build(view, PasaportAdresi(gtin)), "application/ld+json; charset=utf-8");
    }

    [HttpGet("/dpp/{gtin:regex(^\\d{{14}}$)}.png")]
    public async Task<IActionResult> GetQr(string gtin, CancellationToken cancellationToken)
    {
        var product = await BulAsync(gtin, cancellationToken);
        if (product is null)
            return NotFound();

        var png = QrCodeGenerator.Png(PasaportAdresi(gtin));
        return File(png, "image/png");
    }

    [HttpGet("/dpp/{gtin:regex(^\\d{{14}}$)}")]
    public async Task<IActionResult> GetByGtin(
        string gtin,
        [FromQuery] string? rol,
        CancellationToken cancellationToken)
    {
        var product = await BulAsync(gtin, cancellationToken);
        if (product is null)
            return NotFound();

        return Content(DppHtmlRenderer.Render(DppView.From(product, YetkiliMi(rol))), "text/html; charset=utf-8");
    }

    [HttpGet("/api/products")]
    public async Task<IActionResult> GetProducts(CancellationToken cancellationToken)
    {
        var kayitlar = await _db.Products
            .AsNoTracking()
            .OrderBy(product => product.Gtin)
            .Select(product => new
            {
                product.Gtin,
                ModelAdi = product.Batch.Model.Name,
                product.Batch.BatchNo,
                UretimYeri = product.Batch.ProductionPlace,
                product.CottonPercent,
                product.ElastanPercent,
                product.Version,
                product.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var yanit = kayitlar.Select(item => new UrunOzetiDto(
            item.Gtin,
            item.ModelAdi,
            item.BatchNo,
            item.UretimYeri,
            item.CottonPercent,
            item.ElastanPercent,
            item.Version,
            Utc(item.UpdatedAt))).ToList();

        return Ok(yanit);
    }

    [HttpGet("/api/products/{gtin}/versions")]
    public async Task<IActionResult> GetVersions(string gtin, [FromQuery] string? rol, CancellationToken cancellationToken)
    {
        var product = await BulAsync(gtin, cancellationToken);
        if (product is null)
            return NotFound();

        var yetkili = YetkiliMi(rol);
        var eskiler = await _db.ProductVersions
            .Where(version => version.Gtin == gtin)
            .OrderBy(version => version.Version)
            .ToListAsync(cancellationToken);

        var yanit = new UrunSurumleriDto(
            product.Gtin,
            Surum(
                product.Version,
                product.UpdatedAt,
                product.Batch.Model.Name,
                product.Batch.BatchNo,
                product.Batch.ProductionPlace,
                product.CottonPercent,
                product.ElastanPercent,
                product.CarbonKgCo2e,
                yetkili),
            eskiler.Select(version => Surum(
                version.Version,
                version.UpdatedAt,
                version.ModelAdi,
                version.BatchNo,
                version.UretimYeri,
                version.CottonPercent,
                version.ElastanPercent,
                version.CarbonKgCo2e,
                yetkili)).ToList());

        return Ok(yanit);
    }

    private static SurumDto Surum(
        int version,
        DateTime updatedAt,
        string modelAdi,
        string batchNo,
        string uretimYeri,
        decimal pamuk,
        decimal elastan,
        decimal karbon,
        bool yetkili) =>
        new(version, Utc(updatedAt), modelAdi, batchNo, uretimYeri, pamuk, elastan, yetkili ? karbon : null);

    private static DateTime Utc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private string PasaportAdresi(string gtin) =>
        DppUrlBuilder.Build(
            _configuration["PublicBaseUrl"],
            Request.Scheme,
            Request.Host.ToString(),
            Request.PathBase.Value,
            gtin);

    private static bool YetkiliMi(string? rol) =>
        string.Equals(rol, "yetkili", StringComparison.OrdinalIgnoreCase);

    private Task<Product?> BulAsync(string gtin, CancellationToken cancellationToken) =>
        _db.Products
            .Include(item => item.Batch)
            .ThenInclude(batch => batch.Model)
            .FirstOrDefaultAsync(item => item.Gtin == gtin, cancellationToken);
}

public sealed record UrunOzetiDto(
    [property: JsonPropertyName("gtin")] string Gtin,
    [property: JsonPropertyName("modelAdi")] string ModelAdi,
    [property: JsonPropertyName("batchNo")] string BatchNo,
    [property: JsonPropertyName("uretimYeri")] string UretimYeri,
    [property: JsonPropertyName("pamuk")] decimal Pamuk,
    [property: JsonPropertyName("elastan")] decimal Elastan,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt);

public sealed record UrunSurumleriDto(
    [property: JsonPropertyName("gtin")] string Gtin,
    [property: JsonPropertyName("guncel")] SurumDto Guncel,
    [property: JsonPropertyName("eskiSurumler")] IReadOnlyList<SurumDto> EskiSurumler);

public sealed record SurumDto(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("updatedAt")] DateTime UpdatedAt,
    [property: JsonPropertyName("modelAdi")] string ModelAdi,
    [property: JsonPropertyName("batchNo")] string BatchNo,
    [property: JsonPropertyName("uretimYeri")] string UretimYeri,
    [property: JsonPropertyName("pamuk")] decimal Pamuk,
    [property: JsonPropertyName("elastan")] decimal Elastan,
    [property: JsonPropertyName("karbon")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    decimal? Karbon);

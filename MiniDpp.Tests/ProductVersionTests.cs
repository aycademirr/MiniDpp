// GTIN yeniden yüklenince sürüm oluşur; versions ucunda karbon role bağlıdır.

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MiniDpp.Api.Controllers;
using MiniDpp.Api.Data;
using MiniDpp.Api.Services;

namespace MiniDpp.Tests;

public class ProductVersionTests
{
    private const string Baslik =
        "gtin,model_adi,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e";

    private const string Gtin = "08690000000012";

    [Fact]
    public async Task Degisiklik_yoksa_yeni_surum_olusmaz()
    {
        await using var db = CreateDb();
        var csv = Dosya(Veri(Gtin, "98", "2", "12.4"));

        await IceAktar(db, csv);
        var once = await db.Context.Products.AsNoTracking().SingleAsync(Iptal);
        var rapor = await IceAktar(db, csv);
        var sonra = await db.Context.Products.AsNoTracking().SingleAsync(Iptal);

        Assert.Equal(0, rapor.Basarili);
        Assert.Equal(1, rapor.DegisiklikYok);
        Assert.Equal(0, rapor.Hatali);
        Assert.Equal(1, sonra.Version);
        Assert.Equal(once.UpdatedAt, sonra.UpdatedAt);
        Assert.Equal(0, await db.Context.ProductVersions.CountAsync(Iptal));
    }

    [Fact]
    public async Task Deger_degisince_eski_surum_saklanir()
    {
        await using var db = CreateDb();
        await IceAktar(db, Dosya(Veri(Gtin, "98", "2", "12.4")));
        var rapor = await IceAktar(db, Dosya(Veri(Gtin, "97", "3", "11.1")));

        Assert.Equal(1, rapor.Basarili);
        Assert.Equal(1, rapor.Guncellenen);
        var guncel = await db.Context.Products.AsNoTracking().SingleAsync(Iptal);
        var eski = await db.Context.ProductVersions.AsNoTracking().SingleAsync(Iptal);
        Assert.Equal(2, guncel.Version);
        Assert.Equal(97m, guncel.CottonPercent);
        Assert.Equal(3m, guncel.ElastanPercent);
        Assert.Equal(11.1m, guncel.CarbonKgCo2e);
        Assert.Equal(1, eski.Version);
        Assert.Equal(98m, eski.CottonPercent);
        Assert.Equal(2m, eski.ElastanPercent);
        Assert.Equal(12.4m, eski.CarbonKgCo2e);
        Assert.Equal("Slim Denim 501", eski.ModelAdi);
        Assert.Equal("B2027-01", eski.BatchNo);
        Assert.Equal("Denizli", eski.UretimYeri);
    }

    [Fact]
    public async Task Iki_guncellemede_iki_eski_surum_olur()
    {
        await using var db = CreateDb();
        await IceAktar(db, Dosya(Veri(Gtin, "98", "2", "12.4")));
        await IceAktar(db, Dosya(Veri(Gtin, "97", "3", "11.1")));
        await IceAktar(db, Dosya(Veri(Gtin, "96", "4", "10")));

        var surumler = await db.Context.ProductVersions.AsNoTracking().OrderBy(item => item.Version).ToListAsync(Iptal);
        var guncel = await db.Context.Products.AsNoTracking().SingleAsync(Iptal);
        Assert.Equal(3, guncel.Version);
        Assert.Equal([1, 2], surumler.Select(item => item.Version));
        Assert.Equal(98m, surumler[0].CottonPercent);
        Assert.Equal(97m, surumler[1].CottonPercent);
    }

    [Fact]
    public async Task Versions_ucunda_karbon_yalnizca_yetkiliye_aciktir()
    {
        await using var db = CreateDb();
        await IceAktar(db, Dosya(Veri(Gtin, "98", "2", "12.4")));
        await IceAktar(db, Dosya(Veri(Gtin, "97", "3", "11.1")));
        var controller = new DppController(db.Context, new ConfigurationBuilder().Build());

        var rolsuz = JsonSerializer.Serialize((await controller.GetVersions(Gtin, null, Iptal) as OkObjectResult)!.Value);
        var yetkili = JsonSerializer.Serialize((await controller.GetVersions(Gtin, "yetkili", Iptal) as OkObjectResult)!.Value);

        using var rolsuzBelge = JsonDocument.Parse(rolsuz);
        using var yetkiliBelge = JsonDocument.Parse(yetkili);
        Assert.False(rolsuzBelge.RootElement.GetProperty("guncel").TryGetProperty("karbon", out _));
        Assert.False(rolsuzBelge.RootElement.GetProperty("eskiSurumler")[0].TryGetProperty("karbon", out _));
        Assert.Equal(11.1m, yetkiliBelge.RootElement.GetProperty("guncel").GetProperty("karbon").GetDecimal());
        Assert.Equal(12.4m, yetkiliBelge.RootElement.GetProperty("eskiSurumler")[0].GetProperty("karbon").GetDecimal());
        Assert.IsType<NotFoundResult>(await controller.GetVersions("08690000000029", null, Iptal));
    }

    [Fact]
    public async Task Ayni_dosyada_tekrar_eden_gtin_hata_verir()
    {
        await using var db = CreateDb();
        var csv = Dosya(Veri(Gtin, "98", "2", "12.4"), Veri(Gtin, "97", "3", "11.1"));

        var rapor = await IceAktar(db, csv);

        Assert.Equal(1, rapor.Basarili);
        Assert.Equal(0, rapor.Guncellenen);
        Assert.Equal([CsvImportService.GtinDosyadaMesaji], rapor.Hatalar[0].Nedenler);
        Assert.Equal(1, (await db.Context.Products.AsNoTracking().SingleAsync(Iptal)).Version);
        Assert.Equal(0, await db.Context.ProductVersions.CountAsync(Iptal));
    }

    private static string Veri(string gtin, string pamuk, string elastan, string karbon) =>
        string.Join(',', gtin, "Slim Denim 501", "B2027-01", pamuk, elastan, "Denizli", karbon);

    private static string Dosya(params string[] satirlar) =>
        Baslik + "\n" + string.Join('\n', satirlar) + "\n";

    private static async Task<ImportReport> IceAktar(TestDb db, string csv)
    {
        var sonuc = await new CsvImportService(db.Context).ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(csv)), Iptal);
        Assert.Null(sonuc.EksikBasliklar);
        return sonuc.Rapor!;
    }

    private static CancellationToken Iptal => TestContext.Current.CancellationToken;

    private static TestDb CreateDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<DppDbContext>().UseSqlite(connection).Options;
        var context = new DppDbContext(options);
        context.Database.EnsureCreated();
        return new TestDb(context, connection);
    }

    private sealed class TestDb : IAsyncDisposable
    {
        public TestDb(DppDbContext context, SqliteConnection connection)
        {
            Context = context;
            Connection = connection;
        }

        public DppDbContext Context { get; }

        public SqliteConnection Connection { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}

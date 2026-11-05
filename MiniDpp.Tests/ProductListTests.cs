// Ürün listesi karbon içermez, GTIN sırasındadır ve updatedAt UTC (Z) döner.

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

public class ProductListTests
{
    private const string Baslik =
        "gtin,model_adi,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e";

    [Fact]
    public async Task Urun_listesi_karbonsuz_gtin_sirali_ve_utc_tarihlidir()
    {
        await using var db = CreateDb();
        var csv = Baslik + "\n"
            + Veri("08690000000050", "Mom Jean 505", "B2027-05", "97", "3", "Bursa", "12.2") + "\n"
            + Veri("08690000000012", "Slim Denim 501", "B2027-01", "98", "2", "Denizli", "12.4") + "\n";
        var sonuc = await new CsvImportService(db.Context).ImportAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(csv)),
            Iptal);
        Assert.Null(sonuc.EksikBasliklar);

        var controller = new DppController(db.Context, new ConfigurationBuilder().Build());
        var json = JsonSerializer.Serialize((await controller.GetProducts(Iptal) as OkObjectResult)!.Value);
        using var belge = JsonDocument.Parse(json);
        var urunler = belge.RootElement.EnumerateArray().ToList();

        Assert.Equal(2, urunler.Count);
        Assert.DoesNotContain("karbon", json, StringComparison.Ordinal);
        Assert.Equal("08690000000012", urunler[0].GetProperty("gtin").GetString());
        Assert.Equal("08690000000050", urunler[1].GetProperty("gtin").GetString());
        Assert.Equal("Slim Denim 501", urunler[0].GetProperty("modelAdi").GetString());
        Assert.Equal("Denizli", urunler[0].GetProperty("uretimYeri").GetString());
        Assert.EndsWith("Z", urunler[0].GetProperty("updatedAt").GetString());
        Assert.EndsWith("Z", urunler[1].GetProperty("updatedAt").GetString());

        var surumler = JsonSerializer.Serialize(
            (await controller.GetVersions("08690000000012", null, Iptal) as OkObjectResult)!.Value);
        using var surumBelge = JsonDocument.Parse(surumler);
        Assert.EndsWith("Z", surumBelge.RootElement.GetProperty("guncel").GetProperty("updatedAt").GetString());
    }

    private static string Veri(
        string gtin,
        string model,
        string batch,
        string pamuk,
        string elastan,
        string yer,
        string karbon) =>
        string.Join(',', gtin, model, batch, pamuk, elastan, yer, karbon);

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

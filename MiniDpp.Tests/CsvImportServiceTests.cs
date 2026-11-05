// CSV içe aktarma: başlık, satır numarası, GTIN ve parti kuralları.

using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniDpp.Api.Data;
using MiniDpp.Api.Models;
using MiniDpp.Api.Services;

namespace MiniDpp.Tests;

public class CsvImportServiceTests
{
    private const string Baslik =
        "gtin,model_adi,batch_no,elyaf_pamuk_yuzde,elyaf_elastan_yuzde,uretim_yeri,karbon_kg_co2e";

    [Fact]
    public async Task Eksik_basliklar_hangilerinin_eksik_oldugunu_bildirir()
    {
        await using var db = CreateDb();
        var sonuc = await new CsvImportService(db.Context).ImportAsync(Csv("gtin,model_adi\n"), Iptal);

        Assert.NotNull(sonuc.EksikBasliklar);
        Assert.Equal(
            ["batch_no", "elyaf_pamuk_yuzde", "elyaf_elastan_yuzde", "uretim_yeri", "karbon_kg_co2e"],
            sonuc.EksikBasliklar);
        Assert.Null(sonuc.Rapor);
        Assert.Equal(0, await db.Context.Products.CountAsync(Iptal));
    }

    [Fact]
    public async Task Bos_satir_atlanir_ve_sonraki_satir_numarasi_korunur()
    {
        await using var db = CreateDb();
        var csv = Baslik + "\n"
            + Satir("08690000000012", "Slim Denim 501", "B2027-01", "98", "2", "Denizli", "12.4") + "\n"
            + "\n"
            + Satir("", "Wide Leg Denim 503", "B2027-03", "100", "0", "Bursa", "13.1") + "\n";

        var rapor = await IceAktar(db, csv);

        Assert.Equal(2, rapor.ToplamSatir);
        Assert.Equal(1, rapor.Basarili);
        Assert.Equal(1, rapor.Hatali);
        Assert.Equal(4, rapor.Hatalar[0].Satir);
        Assert.Contains("GTIN boş olamaz.", rapor.Hatalar[0].Nedenler);
    }

    [Fact]
    public async Task Tirnak_bom_ve_gtin_metin_olarak_okunur()
    {
        await using var db = CreateDb();
        var csv = Baslik + "\n"
            + "08690000000012,\"Slim, \"\"Denim\"\" 501\",B2027-01,98,2,Denizli,\" 12.4\"\n";
        var bomlu = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();

        var rapor = await IceAktar(db, bomlu);

        Assert.Equal(1, rapor.Basarili);
        var urun = await db.Context.Products.Include(product => product.Batch).ThenInclude(batch => batch.Model).SingleAsync(Iptal);
        Assert.Equal("08690000000012", urun.Gtin);
        Assert.Equal("Slim, \"Denim\" 501", urun.Batch.Model.Name);
        Assert.Equal(12.4m, urun.CarbonKgCo2e);
    }

    [Fact]
    public async Task Hatali_satir_sonraki_gecerli_satiri_engellemez()
    {
        await using var db = CreateDb();
        var csv = Baslik + "\n"
            + Satir("08690000000043", "Skinny Denim 504", "B2027-04", "95", "3", "Denizli", "12.9") + "\n"
            + Satir("08690000000029", "Regular Denim 502", "B2027-02", "99", "1", "Denizli", "11.8") + "\n";

        var rapor = await IceAktar(db, csv);

        Assert.Equal(2, rapor.ToplamSatir);
        Assert.Equal(1, rapor.Basarili);
        Assert.Equal(1, rapor.Hatali);
        Assert.Equal(["Pamuk ve elastan oranlarının toplamı 100 olmalıdır."], rapor.Hatalar[0].Nedenler);
        Assert.Equal("08690000000029", await db.Context.Products.Select(product => product.Gtin).SingleAsync(Iptal));
    }

    [Fact]
    public async Task Ayni_gtin_dosyada_ikinci_kez_gecerse_kaydedilmez()
    {
        await using var db = CreateDb();
        var csv = Baslik + "\n"
            + Satir("08690000000012", "Slim Denim 501", "B2027-01", "98", "2", "Denizli", "12.4") + "\n"
            + Satir("08690000000012", "Regular Denim 502", "B2027-02", "99", "1", "Denizli", "11.8") + "\n";

        var rapor = await IceAktar(db, csv);

        Assert.Equal(1, rapor.Basarili);
        Assert.Equal([CsvImportService.GtinDosyadaMesaji], rapor.Hatalar[0].Nedenler);
        Assert.Equal(1, await db.Context.Products.CountAsync(Iptal));
    }

    [Fact]
    public async Task Hatali_satirdaki_gtin_sonraki_gecerli_satirda_kaydedilir()
    {
        await using var db = CreateDb();
        var csv = Baslik + "\n"
            + Satir("08690000000012", "Slim Denim 501", "B2027-01", "95", "3", "Denizli", "12.4") + "\n"
            + Satir("08690000000012", "Slim Denim 501", "B2027-01", "98", "2", "Denizli", "12.4") + "\n";

        var rapor = await IceAktar(db, csv);

        Assert.Equal(1, rapor.Basarili);
        Assert.Equal(1, rapor.Hatali);
        Assert.Equal(["Pamuk ve elastan oranlarının toplamı 100 olmalıdır."], rapor.Hatalar[0].Nedenler);
        var urun = await db.Context.Products.SingleAsync(Iptal);
        Assert.Equal("08690000000012", urun.Gtin);
        Assert.Equal(98m, urun.CottonPercent);
    }

    [Fact]
    public async Task Ayni_dosyanin_ikinci_yuklemesi_degisiklik_yok_sayar()
    {
        await using var db = CreateDb();
        var csv = Baslik + "\n"
            + Satir("08690000000012", "Slim Denim 501", "B2027-01", "98", "2", "Denizli", "12.4") + "\n"
            + Satir("08690000000029", "Regular Denim 502", "B2027-02", "99", "1", "Denizli", "11.8") + "\n"
            + Satir("", "Wide Leg Denim 503", "B2027-03", "100", "0", "Bursa", "13.1") + "\n"
            + Satir("08690000000043", "Skinny Denim 504", "B2027-04", "95", "3", "Denizli", "12.9") + "\n"
            + Satir("08690000000050", "Mom Jean 505", "B2027-05", "97", "3", "Bursa", "12.2") + "\n";

        var ilk = await IceAktar(db, csv);
        var ikinci = await IceAktar(db, csv);

        Assert.Equal(3, ilk.Basarili);
        Assert.Equal(2, ilk.Hatali);
        Assert.Equal(0, ikinci.Basarili);
        Assert.Equal(0, ikinci.Guncellenen);
        Assert.Equal(3, ikinci.DegisiklikYok);
        Assert.Equal(2, ikinci.Hatali);
        Assert.Equal(5, ikinci.ToplamSatir);
        Assert.Equal(ikinci.ToplamSatir, ikinci.Basarili + ikinci.DegisiklikYok + ikinci.Hatali);
        Assert.Equal(0, await db.Context.ProductVersions.CountAsync(Iptal));
    }

    [Fact]
    public async Task Farkli_uretim_yeri_cakisma_hatasi_verir_ve_kaydetmez()
    {
        await using var db = CreateDb();
        var csv = Baslik + "\n"
            + Satir("08690000000012", "Slim Denim 501", "B2027-01", "98", "2", "Denizli", "12.4") + "\n"
            + Satir("08690000000029", "Slim Denim 501", "B2027-01", "99", "1", "Bursa", "11.8") + "\n";

        var rapor = await IceAktar(db, csv);

        Assert.Equal(1, rapor.Basarili);
        Assert.Equal([CsvImportService.UretimYeriCakismaMesaji], rapor.Hatalar[0].Nedenler);
        var parti = await db.Context.Batches.SingleAsync(Iptal);
        Assert.Equal("Denizli", parti.ProductionPlace);
        Assert.Equal(1, await db.Context.Products.CountAsync(Iptal));
    }

    [Fact]
    public async Task Ayni_model_ve_parti_ayni_yerde_yeniden_kullanilir()
    {
        await using var db = CreateDb();
        var csv = Baslik + "\n"
            + Satir("08690000000012", "Slim Denim 501", "B2027-01", "98", "2", "Denizli", "12.4") + "\n"
            + Satir("08690000000050", "Slim Denim 501", "B2027-01", "97", "3", "Denizli", "12.2") + "\n";

        var rapor = await IceAktar(db, csv);

        Assert.Equal(2, rapor.Basarili);
        Assert.Empty(rapor.Hatalar);
        Assert.Equal(1, await db.Context.ProductModels.CountAsync(Iptal));
        Assert.Equal(1, await db.Context.Batches.CountAsync(Iptal));
        Assert.Equal(2, await db.Context.Products.CountAsync(Iptal));
    }

    private static async Task<ImportReport> IceAktar(TestDb db, string csv) =>
        await IceAktar(db, Encoding.UTF8.GetBytes(csv));

    private static async Task<ImportReport> IceAktar(TestDb db, byte[] csv)
    {
        var sonuc = await new CsvImportService(db.Context).ImportAsync(new MemoryStream(csv), Iptal);
        Assert.Null(sonuc.EksikBasliklar);
        return sonuc.Rapor!;
    }

    private static string Satir(
        string gtin,
        string model,
        string batch,
        string pamuk,
        string elastan,
        string yer,
        string karbon) =>
        string.Join(',', gtin, model, batch, pamuk, elastan, yer, karbon);

    private static MemoryStream Csv(string text) => new(Encoding.UTF8.GetBytes(text));

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

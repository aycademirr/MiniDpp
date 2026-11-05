// CSV dosyasını okur. Yeni GTIN eklenir; değişen GTIN yeni sürüme geçer, aynı değerler dokunulmaz.

using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MiniDpp.Api.Data;
using MiniDpp.Api.Models;

namespace MiniDpp.Api.Services;

public sealed class CsvImportService
{
    public const string GtinDosyadaMesaji = "Bu GTIN dosyada daha önce geçti";
    public const string UretimYeriCakismaMesaji =
        "Üretim yeri çakışması: bu model ve parti numarası farklı bir üretim yeri ile kayıtlı.";

    private static readonly string[] Basliklar =
    [
        "gtin",
        "model_adi",
        "batch_no",
        "elyaf_pamuk_yuzde",
        "elyaf_elastan_yuzde",
        "uretim_yeri",
        "karbon_kg_co2e"
    ];

    private readonly DppDbContext _db;

    public CsvImportService(DppDbContext db)
    {
        _db = db;
    }

    public async Task<CsvImportResult> ImportAsync(Stream csv, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(
            csv,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        var records = CsvTablo.Oku(text);

        if (records.Count == 0 || !records[0].Dolu)
            return CsvImportResult.Eksik(Basliklar);

        var sutunlar = BaslikIndeksleri(records[0].Alanlar);
        var eksikler = Basliklar.Where(baslik => !sutunlar.ContainsKey(baslik)).ToArray();
        if (eksikler.Length > 0)
            return CsvImportResult.Eksik(eksikler);

        var hatalar = new List<ImportSatirHatasi>();
        var gorulenGtinler = new HashSet<string>(StringComparer.Ordinal);
        var yeni = 0;
        var guncellenen = 0;
        var degisiklikYok = 0;

        foreach (var kayit in records.Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var gtin = Hucre(kayit.Alanlar, sutunlar, "gtin");
            var modelAdi = Hucre(kayit.Alanlar, sutunlar, "model_adi");
            var batchNo = Hucre(kayit.Alanlar, sutunlar, "batch_no");
            var pamuk = Hucre(kayit.Alanlar, sutunlar, "elyaf_pamuk_yuzde");
            var elastan = Hucre(kayit.Alanlar, sutunlar, "elyaf_elastan_yuzde");
            var uretimYeri = Hucre(kayit.Alanlar, sutunlar, "uretim_yeri");
            var karbon = Hucre(kayit.Alanlar, sutunlar, "karbon_kg_co2e");

            var dogrulama = ProductRowValidator.Validate(gtin, modelAdi, batchNo, pamuk, elastan, uretimYeri, karbon);
            var nedenler = dogrulama.Errors.ToList();

            var gtinAnahtari = gtin?.Trim();
            if (!string.IsNullOrEmpty(gtinAnahtari) && gorulenGtinler.Contains(gtinAnahtari))
                nedenler.Add(GtinDosyadaMesaji);

            var model = BosluklariAt(modelAdi);
            var parti = BosluklariAt(batchNo);
            var yer = BosluklariAt(uretimYeri);
            ProductModel? kayitliModel = null;
            Batch? kayitliParti = null;

            if (model is not null && parti is not null && yer is not null)
            {
                kayitliModel = await _db.ProductModels
                    .FirstOrDefaultAsync(item => item.Name == model, cancellationToken);
                if (kayitliModel is not null)
                {
                    kayitliParti = await _db.Batches.FirstOrDefaultAsync(
                        item => item.ModelId == kayitliModel.Id && item.BatchNo == parti,
                        cancellationToken);
                    if (kayitliParti is not null && kayitliParti.ProductionPlace != yer)
                        nedenler.Add(UretimYeriCakismaMesaji);
                }
            }

            if (nedenler.Count > 0)
            {
                hatalar.Add(new ImportSatirHatasi(kayit.Satir, nedenler));
                continue;
            }

            try
            {
                var values = dogrulama.Values!.Value;
                switch (await KaydetAsync(values, kayitliModel, kayitliParti, cancellationToken))
                {
                    case KayitSonucu.Yeni:
                        yeni++;
                        break;
                    case KayitSonucu.Guncellendi:
                        guncellenen++;
                        break;
                    case KayitSonucu.DegisiklikYok:
                        degisiklikYok++;
                        break;
                }

                gorulenGtinler.Add(values.Gtin);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _db.ChangeTracker.Clear();
                hatalar.Add(new ImportSatirHatasi(kayit.Satir, ["Kayıt sırasında beklenmeyen bir hata oluştu."]));
            }
        }

        var basarili = yeni + guncellenen;
        var toplam = basarili + degisiklikYok + hatalar.Count;
        return CsvImportResult.Tamam(new ImportReport(toplam, basarili, hatalar.Count, hatalar, guncellenen, degisiklikYok));
    }

    private async Task<KayitSonucu> KaydetAsync(
        ParsedProductRow values,
        ProductModel? model,
        Batch? batch,
        CancellationToken cancellationToken)
    {
        var mevcut = await _db.Products
            .Include(product => product.Batch)
            .ThenInclude(item => item.Model)
            .FirstOrDefaultAsync(product => product.Gtin == values.Gtin, cancellationToken);

        if (mevcut is not null && AyniDegerler(mevcut, values))
            return KayitSonucu.DegisiklikYok;

        if (model is null)
        {
            model = new ProductModel { Name = values.ModelAdi };
            _db.ProductModels.Add(model);
        }

        if (batch is null)
        {
            batch = new Batch
            {
                BatchNo = values.BatchNo,
                ProductionPlace = values.UretimYeri,
                Model = model
            };
            _db.Batches.Add(batch);
        }

        if (mevcut is null)
        {
            _db.Products.Add(new Product
            {
                Gtin = values.Gtin,
                CottonPercent = values.Pamuk,
                ElastanPercent = values.Elastan,
                CarbonKgCo2e = values.Karbon,
                Version = 1,
                UpdatedAt = DateTime.UtcNow,
                Batch = batch
            });
            await _db.SaveChangesAsync(cancellationToken);
            return KayitSonucu.Yeni;
        }

        _db.ProductVersions.Add(new ProductVersion
        {
            Gtin = mevcut.Gtin,
            Version = mevcut.Version,
            ModelAdi = mevcut.Batch.Model.Name,
            BatchNo = mevcut.Batch.BatchNo,
            UretimYeri = mevcut.Batch.ProductionPlace,
            CottonPercent = mevcut.CottonPercent,
            ElastanPercent = mevcut.ElastanPercent,
            CarbonKgCo2e = mevcut.CarbonKgCo2e,
            UpdatedAt = mevcut.UpdatedAt
        });

        mevcut.CottonPercent = values.Pamuk;
        mevcut.ElastanPercent = values.Elastan;
        mevcut.CarbonKgCo2e = values.Karbon;
        mevcut.Batch = batch;
        mevcut.Version++;
        mevcut.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return KayitSonucu.Guncellendi;
    }

    private static bool AyniDegerler(Product mevcut, ParsedProductRow values) =>
        mevcut.Batch.Model.Name == values.ModelAdi
        && mevcut.Batch.BatchNo == values.BatchNo
        && mevcut.Batch.ProductionPlace == values.UretimYeri
        && mevcut.CottonPercent == values.Pamuk
        && mevcut.ElastanPercent == values.Elastan
        && mevcut.CarbonKgCo2e == values.Karbon;

    private static Dictionary<string, int> BaslikIndeksleri(IReadOnlyList<string> alanlar)
    {
        var sutunlar = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < alanlar.Count; i++)
        {
            var ad = alanlar[i].Trim().TrimStart('\uFEFF');
            if (ad.Length > 0)
                sutunlar.TryAdd(ad, i);
        }

        return sutunlar;
    }

    private static string? Hucre(IReadOnlyList<string> alanlar, IReadOnlyDictionary<string, int> sutunlar, string ad)
    {
        if (!sutunlar.TryGetValue(ad, out var indeks) || indeks >= alanlar.Count)
            return null;

        return alanlar[indeks];
    }

    private static string? BosluklariAt(string? deger)
    {
        if (string.IsNullOrWhiteSpace(deger))
            return null;

        return deger.Trim();
    }
}

public sealed class CsvImportResult
{
    private CsvImportResult(IReadOnlyList<string>? eksikBasliklar, ImportReport? rapor)
    {
        EksikBasliklar = eksikBasliklar;
        Rapor = rapor;
    }

    public IReadOnlyList<string>? EksikBasliklar { get; }

    public ImportReport? Rapor { get; }

    public static CsvImportResult Eksik(IReadOnlyList<string> basliklar) => new(basliklar, null);

    public static CsvImportResult Tamam(ImportReport rapor) => new(null, rapor);
}

public sealed record ImportReport(
    [property: JsonPropertyName("toplamSatir")] int ToplamSatir,
    [property: JsonPropertyName("basarili")] int Basarili,
    [property: JsonPropertyName("hatali")] int Hatali,
    [property: JsonPropertyName("hatalar")] IReadOnlyList<ImportSatirHatasi> Hatalar,
    [property: JsonPropertyName("guncellenen")] int Guncellenen,
    [property: JsonPropertyName("degisiklikYok")] int DegisiklikYok);

enum KayitSonucu
{
    Yeni,
    Guncellendi,
    DegisiklikYok
}

public sealed record ImportSatirHatasi(
    [property: JsonPropertyName("satir")] int Satir,
    [property: JsonPropertyName("nedenler")] IReadOnlyList<string> Nedenler);

// Başlık her zaman 1. satırdır. Tırnak içindeki virgül ve çift tırnak alanın parçasıdır.
file static class CsvTablo
{
    public static List<CsvKayit> Oku(string text)
    {
        var kayitlar = new List<CsvKayit>();
        if (text.Length == 0)
            return kayitlar;

        var alanlar = new List<string>();
        var alan = new StringBuilder();
        var tirnakIcinde = false;
        var dolu = false;
        var baslikAlindi = false;
        var satir = 1;
        var kayitSatiri = 1;

        void Bitir()
        {
            alanlar.Add(alan.ToString());
            if (!baslikAlindi || dolu)
                kayitlar.Add(new CsvKayit(kayitSatiri, alanlar.ToArray(), dolu));

            baslikAlindi = true;
            alanlar.Clear();
            alan.Clear();
            dolu = false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var karakter = text[i];
            if (tirnakIcinde)
            {
                if (karakter == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        alan.Append('"');
                        i++;
                    }
                    else
                    {
                        tirnakIcinde = false;
                    }
                }
                else if (karakter is '\r' or '\n')
                {
                    if (karakter == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    alan.Append('\n');
                    satir++;
                }
                else
                {
                    alan.Append(karakter);
                }

                continue;
            }

            if (karakter == '"')
            {
                tirnakIcinde = true;
                dolu = true;
                continue;
            }

            if (karakter == ',')
            {
                alanlar.Add(alan.ToString());
                alan.Clear();
                dolu = true;
                continue;
            }

            if (karakter is '\r' or '\n')
            {
                if (karakter == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;

                Bitir();
                satir++;
                kayitSatiri = satir;
                continue;
            }

            alan.Append(karakter);
            if (!char.IsWhiteSpace(karakter))
                dolu = true;
        }

        if (dolu || alan.Length > 0 || alanlar.Count > 0)
            Bitir();

        return kayitlar;
    }
}

file sealed record CsvKayit(int Satir, string[] Alanlar, bool Dolu);

// CSV satırları ve satır doğrulama kuralları.

using MiniDpp.Api.Services;

namespace MiniDpp.Tests;

public class ProductRowValidatorTests
{
    [Theory]
    [InlineData("08690000000012", "Slim Denim 501", "B2027-01", "98", "2", "Denizli", "12.4", 98, 2, 12.4)]
    [InlineData("08690000000029", "Regular Denim 502", "B2027-02", "99", "1", "Denizli", "11.8", 99, 1, 11.8)]
    [InlineData("08690000000050", "Mom Jean 505", "B2027-05", "97", "3", "Bursa", "12.2", 97, 3, 12.2)]
    public void Gecerli_csv_satirlari_pamuk_ve_elastan_toplami_100(
        string gtin,
        string modelAdi,
        string batchNo,
        string pamuk,
        string elastan,
        string uretimYeri,
        string karbon,
        int beklenenPamuk,
        int beklenenElastan,
        double beklenenKarbon)
    {
        var result = ProductRowValidator.Validate(gtin, modelAdi, batchNo, pamuk, elastan, uretimYeri, karbon);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Values);

        var values = result.Values.Value;
        Assert.Equal(gtin, values.Gtin);
        Assert.Equal(modelAdi, values.ModelAdi);
        Assert.Equal(batchNo, values.BatchNo);
        Assert.Equal(beklenenPamuk, values.Pamuk);
        Assert.Equal(beklenenElastan, values.Elastan);
        Assert.Equal(uretimYeri, values.UretimYeri);
        Assert.Equal((decimal)beklenenKarbon, values.Karbon);
        Assert.Equal(100m, values.Pamuk + values.Elastan);
    }

    [Fact]
    public void Bos_gtin_satiri_gecersiz()
    {
        var result = ProductRowValidator.Validate(
            "",
            "Wide Leg Denim 503",
            "B2027-03",
            "100",
            "0",
            "Bursa",
            "13.1");

        Assert.False(result.IsValid);
        Assert.Null(result.Values);
        Assert.Equal(["GTIN boş olamaz."], result.Errors);
    }

    [Fact]
    public void Pamuk_95_elastan_3_satiri_toplam_hatasi_verir()
    {
        var result = ProductRowValidator.Validate(
            "08690000000043",
            "Skinny Denim 504",
            "B2027-04",
            "95",
            "3",
            "Denizli",
            "12.9");

        Assert.False(result.IsValid);
        Assert.Null(result.Values);
        Assert.Equal(["Pamuk ve elastan oranlarının toplamı 100 olmalıdır."], result.Errors);
    }

    [Fact]
    public void Virgullu_karbon_degeri_gecersizdir()
    {
        var result = ProductRowValidator.Validate(
            "08690000000012",
            "Slim Denim 501",
            "B2027-01",
            "98",
            "2",
            "Denizli",
            "12,4");

        Assert.False(result.IsValid);
        Assert.Contains("Karbon değeri sayı olmalıdır.", result.Errors);
    }

    [Fact]
    public void Noktali_karbon_degeri_gecerlidir()
    {
        var result = ProductRowValidator.Validate(
            "08690000000012",
            "Slim Denim 501",
            "B2027-01",
            "98",
            "2",
            "Denizli",
            "12.4");

        Assert.True(result.IsValid);
        Assert.Equal(12.4m, result.Values!.Value.Karbon);
    }

    [Theory]
    [InlineData(" 12.4")]
    [InlineData("12.4 ")]
    public void Bosluklu_noktali_karbon_degeri_gecerlidir(string karbon)
    {
        var result = ProductRowValidator.Validate(
            "08690000000012",
            "Slim Denim 501",
            "B2027-01",
            "98",
            "2",
            "Denizli",
            karbon);

        Assert.True(result.IsValid);
        Assert.Equal(12.4m, result.Values!.Value.Karbon);
    }

    [Theory]
    [InlineData("1.234,5")]
    [InlineData("abc")]
    public void Binlik_ayracli_veya_metin_karbon_degeri_gecersizdir(string karbon)
    {
        var result = ProductRowValidator.Validate(
            "08690000000012",
            "Slim Denim 501",
            "B2027-01",
            "98",
            "2",
            "Denizli",
            karbon);

        Assert.False(result.IsValid);
        Assert.Contains("Karbon değeri sayı olmalıdır.", result.Errors);
    }

    [Fact]
    public void Birden_fazla_hata_olan_satirda_tum_hatalar_listelenir()
    {
        var result = ProductRowValidator.Validate(
            "12A",
            " ",
            "",
            "150",
            "x",
            null,
            "-1");

        Assert.False(result.IsValid);
        Assert.Null(result.Values);

        string[] beklenenHatalar =
        [
            "GTIN 14 haneli olmalıdır.",
            "GTIN yalnızca rakamlardan oluşmalıdır.",
            "Model adı boş olamaz.",
            "Parti numarası boş olamaz.",
            "Üretim yeri boş olamaz.",
            "Pamuk oranı 0 ile 100 arasında olmalıdır.",
            "Elastan oranı sayı olmalıdır.",
            "Karbon değeri negatif olamaz."
        ];

        Assert.Equal(beklenenHatalar.Length, result.Errors.Count);
        foreach (var hata in beklenenHatalar)
            Assert.Contains(hata, result.Errors);
    }

    [Fact]
    public void Negatif_karbon_gecersizdir()
    {
        var result = ProductRowValidator.Validate(
            "08690000000012",
            "Slim Denim 501",
            "B2027-01",
            "98",
            "2",
            "Denizli",
            "-1");

        Assert.False(result.IsValid);
        Assert.Equal(["Karbon değeri negatif olamaz."], result.Errors);
    }

    [Theory]
    [InlineData("101", "0", "Pamuk oranı 0 ile 100 arasında olmalıdır.")]
    [InlineData("-1", "101", "Pamuk oranı 0 ile 100 arasında olmalıdır.")]
    [InlineData("0", "101", "Elastan oranı 0 ile 100 arasında olmalıdır.")]
    [InlineData("101", "-1", "Elastan oranı 0 ile 100 arasında olmalıdır.")]
    public void Yuzde_0_ile_100_disindaysa_gecersizdir(string pamuk, string elastan, string beklenenHata)
    {
        var result = ProductRowValidator.Validate(
            "08690000000012",
            "Slim Denim 501",
            "B2027-01",
            pamuk,
            elastan,
            "Denizli",
            "12.4");

        Assert.False(result.IsValid);
        Assert.Contains(beklenenHata, result.Errors);
    }
}

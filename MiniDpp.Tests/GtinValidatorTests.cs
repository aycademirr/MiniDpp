// GTIN-14 biçim ve kontrol basamağı kuralları.

using MiniDpp.Api.Services;

namespace MiniDpp.Tests;

public class GtinValidatorTests
{
    [Fact]
    public void On_uc_haneli_gtin_gecersizdir()
    {
        var errors = GtinValidator.Validate("0869000000001");

        Assert.Equal(["GTIN 14 haneli olmalıdır."], errors);
    }

    [Fact]
    public void Harf_iceren_gtin_gecersizdir()
    {
        var errors = GtinValidator.Validate("0869000000001A");

        Assert.Equal(["GTIN yalnızca rakamlardan oluşmalıdır."], errors);
    }

    [Fact]
    public void Kontrol_basamagi_yanlis_olan_14_haneli_gtin_gecersizdir()
    {
        var errors = GtinValidator.Validate("08690000000011");

        Assert.Equal(["GTIN kontrol basamağı geçersiz."], errors);
    }

    [Fact]
    public void Kontrol_basamagi_sifir_olan_gtin_gecerlidir()
    {
        var errors = GtinValidator.Validate("08690000000050");

        Assert.Empty(errors);
    }
}

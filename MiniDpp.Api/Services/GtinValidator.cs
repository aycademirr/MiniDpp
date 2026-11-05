// GTIN-14 biçimini ve GS1 kontrol basamağını denetler.

namespace MiniDpp.Api.Services;

public static class GtinValidator
{
    public static IReadOnlyList<string> Validate(string? gtin)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(gtin))
        {
            errors.Add("GTIN boş olamaz.");
            return errors;
        }

        var value = gtin.Trim();

        if (value.Length != 14)
            errors.Add("GTIN 14 haneli olmalıdır.");

        // Unicode rakamları (ör. ¹) kontrol hesabını bozar; yalnız 0-9 kabul edilir.
        if (value.Any(digit => digit is < '0' or > '9'))
            errors.Add("GTIN yalnızca rakamlardan oluşmalıdır.");

        if (errors.Count == 0 && !HasValidCheckDigit(value))
            errors.Add("GTIN kontrol basamağı geçersiz.");

        return errors;
    }

    private static bool HasValidCheckDigit(string gtin)
    {
        var sum = 0;

        for (var i = 0; i < 13; i++)
        {
            var digit = gtin[i] - '0';
            var weight = i % 2 == 0 ? 3 : 1; // soldan 3, 1, 3, 1...
            sum += digit * weight;
        }

        var expected = (10 - (sum % 10)) % 10; // toplam 10'un katıysa kontrol 0 olur
        return gtin[13] - '0' == expected;
    }
}

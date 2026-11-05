// Ham ürün satırını doğrular; geçerliyse sayıya çevrilmiş değerleri, değilse tüm hataları döndürür.

using System.Globalization;

namespace MiniDpp.Api.Services;

public static class ProductRowValidator
{
    public static ProductRowValidationResult Validate(
        string? gtin,
        string? modelAdi,
        string? batchNo,
        string? pamuk,
        string? elastan,
        string? uretimYeri,
        string? karbon)
    {
        var errors = new List<string>();
        errors.AddRange(GtinValidator.Validate(gtin));

        var model = RequireText(modelAdi, "Model adı boş olamaz.", errors);
        var batch = RequireText(batchNo, "Parti numarası boş olamaz.", errors);
        var place = RequireText(uretimYeri, "Üretim yeri boş olamaz.", errors);

        var cotton = ParsePercent(pamuk, "Pamuk", errors);
        var elastane = ParsePercent(elastan, "Elastan", errors);

        // İkisi de sayıya dönmeden toplam kontrolü anlamsızdır.
        if (cotton is not null && elastane is not null && cotton.Value + elastane.Value != 100m)
            errors.Add("Pamuk ve elastan oranlarının toplamı 100 olmalıdır.");

        var carbon = ParseCarbon(karbon, errors);

        if (errors.Count > 0)
            return ProductRowValidationResult.Invalid(errors);

        return ProductRowValidationResult.Valid(new ParsedProductRow(
            gtin!.Trim(),
            model!,
            batch!,
            cotton!.Value,
            elastane!.Value,
            place!,
            carbon!.Value));
    }

    private static string? RequireText(string? raw, string emptyMessage, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            errors.Add(emptyMessage);
            return null;
        }

        return raw.Trim();
    }

    private static decimal? ParsePercent(string? raw, string fieldName, List<string> errors)
    {
        if (!TryParseDecimal(raw, out var value))
        {
            errors.Add($"{fieldName} oranı sayı olmalıdır.");
            return null;
        }

        if (value < 0m || value > 100m)
            errors.Add($"{fieldName} oranı 0 ile 100 arasında olmalıdır.");

        return value;
    }

    private static decimal? ParseCarbon(string? raw, List<string> errors)
    {
        if (!TryParseDecimal(raw, out var value))
        {
            errors.Add("Karbon değeri sayı olmalıdır.");
            return null;
        }

        if (value < 0m)
            errors.Add("Karbon değeri negatif olamaz.");

        return value;
    }

    private static bool TryParseDecimal(string? raw, out decimal value)
    {
        // Virgül binlik ayraç sayılmasın: "12,4" geçersiz, "12.4" geçerli.
        const NumberStyles styles =
            NumberStyles.AllowLeadingWhite
            | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign
            | NumberStyles.AllowDecimalPoint;

        return decimal.TryParse(raw, styles, CultureInfo.InvariantCulture, out value);
    }
}

public readonly record struct ParsedProductRow(
    string Gtin,
    string ModelAdi,
    string BatchNo,
    decimal Pamuk,
    decimal Elastan,
    string UretimYeri,
    decimal Karbon);

public sealed class ProductRowValidationResult
{
    private ProductRowValidationResult(ParsedProductRow? values, IReadOnlyList<string> errors)
    {
        Values = values;
        Errors = errors;
    }

    public ParsedProductRow? Values { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Values is not null;

    public static ProductRowValidationResult Valid(ParsedProductRow values) =>
        new(values, []);

    public static ProductRowValidationResult Invalid(IReadOnlyList<string> errors) =>
        new(null, errors);
}

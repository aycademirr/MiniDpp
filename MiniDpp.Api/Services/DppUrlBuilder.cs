// Tüketici pasaport adresini üretir. QR ve JSON-LD bu adresi kullanır; rol parametresi eklenmez.

namespace MiniDpp.Api.Services;

public static class DppUrlBuilder
{
    public static string Build(string? publicBaseUrl, string scheme, string host, string? pathBase, string gtin)
    {
        var istekTabani = $"{scheme}://{host}{pathBase}".TrimEnd('/');
        var taban = string.IsNullOrWhiteSpace(publicBaseUrl)
            ? istekTabani
            : publicBaseUrl.Trim().TrimEnd('/');

        return $"{taban}/dpp/{gtin}";
    }
}

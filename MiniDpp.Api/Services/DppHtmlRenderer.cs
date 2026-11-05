// Ürün pasaportunun HTML sayfasını DppView'dan üretir. Karbon satırı yalnızca değer varsa yazılır.

using System.Globalization;
using System.Net;

namespace MiniDpp.Api.Services;

public static class DppHtmlRenderer
{
    public static string Render(DppView view)
    {
        var model = Kodla(view.ModelAdi);
        var gtin = Kodla(view.Gtin);
        var parti = Kodla(view.BatchNo);
        var yer = Kodla(view.UretimYeri);
        var pamuk = Kodla(view.PamukMetni);
        var elastan = Kodla(view.ElastanMetni);
        var pamukOlcu = view.Pamuk.ToString("0.##", CultureInfo.InvariantCulture);
        var elastanOlcu = view.Elastan.ToString("0.##", CultureInfo.InvariantCulture);
        var karbonKarti = view.Karbon is decimal karbon
            ? $"""

                <article class="kart">
                  <h2>Karbon (kg CO2e)</h2>
                  <p>{Kodla(karbon.ToString("0.##", CultureInfo.InvariantCulture))}</p>
                </article>
            """
            : "";

        return $$"""
            <!DOCTYPE html>
            <html lang="tr">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Ürün pasaportu — {{model}}</title>
              <style>
                * { box-sizing: border-box; }
                html, body { margin: 0; max-width: 100%; overflow-x: clip; }
                body { font-family: system-ui, "Segoe UI", sans-serif; line-height: 1.45; background: #eef3f1; color: #14211e; }
                header { background: #0b3a32; color: #fff; }
                .ust, main { width: min(100%, 40rem); margin: 0 auto; }
                .ust { padding: 1.45rem 1rem 1.7rem; }
                header p { margin: 0; color: #b7d4cb; font-size: 0.78rem; font-weight: 650; letter-spacing: 0.04em; }
                h1 { margin: 0.35rem 0 0; font-size: clamp(1.55rem, 6vw, 2.05rem); line-height: 1.15; letter-spacing: -0.03em; overflow-wrap: anywhere; }
                main { padding: 0 1rem 2.25rem; margin-top: -0.85rem; }
                .kartlar { display: grid; gap: 0.65rem; }
                .kart, .elyaf { min-width: 0; background: #fff; border: 1px solid #d7e4df; border-radius: 16px; padding: 0.9rem 1rem; }
                .elyaf { margin-top: 0.65rem; }
                h2 { margin: 0; font-size: 0.75rem; font-weight: 650; color: #5f736c; }
                .kart p { margin: 0.2rem 0 0; font-size: 1.05rem; overflow-wrap: anywhere; }
                .cubuk { display: flex; height: 0.75rem; margin-top: 0.8rem; border-radius: 999px; overflow: hidden; background: #e7eeeb; }
                .pamuk { background: #0f766e; }
                .elastan { background: #14c4a3; }
                .oranlar { display: flex; justify-content: space-between; gap: 0.75rem; margin: 0.55rem 0 0; font-size: 0.92rem; }
                .oranlar span { min-width: 0; overflow-wrap: anywhere; }
              </style>
            </head>
            <body>
              <header>
                <div class="ust">
                  <p>Dijital ürün pasaportu</p>
                  <h1>{{model}}</h1>
                </div>
              </header>
              <main>
                <section class="kartlar">
                  <article class="kart">
                    <h2>GTIN</h2>
                    <p>{{gtin}}</p>
                  </article>
                  <article class="kart">
                    <h2>Parti numarası</h2>
                    <p>{{parti}}</p>
                  </article>
                  <article class="kart">
                    <h2>Üretim yeri</h2>
                    <p>{{yer}}</p>
                  </article>{{karbonKarti}}
                </section>
                <section class="elyaf">
                  <h2>Elyaf</h2>
                  <div class="cubuk" role="img" aria-label="Pamuk {{pamuk}}, elastan {{elastan}}">
                    <span class="pamuk" style="width:{{pamukOlcu}}%"></span>
                    <span class="elastan" style="width:{{elastanOlcu}}%"></span>
                  </div>
                  <p class="oranlar"><span>Pamuk {{pamuk}}</span><span>Elastan {{elastan}}</span></p>
                </section>
              </main>
            </body>
            </html>
            """;
    }

    private static string Kodla(string value) => WebUtility.HtmlEncode(value);
}

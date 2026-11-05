# Mini DPP — Dijital Ürün Pasaportu


Mini DPP, bir CSV dosyasındaki denim ürün satırlarını doğrular, geçerli kayıtları SQLite veritabanına yazar ve her GTIN için HTML ürün pasaportu, schema.org JSON-LD belgesi ve QR kodu üretir. Hatalı satırlar reddedilir; geçerli satırlar kaydedilmeye devam eder.

## Teknolojiler

- ASP.NET Core (.NET 10)
- C#
- Entity Framework Core (SQLite sağlayıcısı)
- SQLite
- HTML, CSS ve JavaScript (ayrı bir ön yüz çerçevesi yoktur)
- xUnit
- QRCoder (PNG QR üretimi)

## Özellikler

- CSV içe aktarma (`POST /api/import`)
- GTIN-14 biçimi ve GS1 kontrol basamağı doğrulaması
- Pamuk ve elastan yüzdesi doğrulaması
- Zorunlu alan kontrolü
- Hatalı satırları reddedip geçerli satırları kaydetme
- Model, parti ve ürün ilişkisi
- HTML ürün pasaportu
- schema.org/Product JSON-LD
- QR PNG
- Demo tüketici ve yetkili görünürlüğü
- Ürün sürüm geçmişi
- Yönetim paneli ve veri kalitesi ekranı
- Türkiye üretim haritası
- Kayıtlı ürünleri CSV olarak dışa aktarma

## Kurulum ve çalıştırma

**Gereksinim:** .NET 10 SDK.

Projeyi GitHub üzerinden klonlamak için:

```bash
git clone https://github.com/aycademirr/MiniDpp.git
cd MiniDpp
```

Proje ana klasöründe aşağıdaki komutları çalıştırın:

```bash
dotnet restore
cd MiniDpp.Api
dotnet run --urls http://localhost:5118
```

Uygulama başlatıldıktan sonra tarayıcıdan aşağıdaki adrese erişilebilir:

[http://localhost:5118](http://localhost:5118)

## Hızlı demo

1. Uygulamayı yukarıdaki komutla başlatın. Veritabanı boşsa panelde ürün yoktur. `GET /api/products` yanıtı `[]` olur.
2. `ornek/urunler_duzeltilmis_test.csv` dosyasını yükleyin. Panelde **CSV İçe Aktarma** ekranından dosyayı seçip **Yükle** düğmesine basabilir veya repo kökünden şu komutu kullanabilirsiniz.

Windows cmd:

```bat
curl.exe -F "file=@ornek\urunler_duzeltilmis_test.csv" http://localhost:5118/api/import
```

bash:

```bash
curl -F "file=@ornek/urunler_duzeltilmis_test.csv" http://localhost:5118/api/import
```

3. Dosyada başlık dışında 5 satır vardır. İlk yüklemede 3 satır kaydedilir, 2 satır reddedilir:
   - Satır 4: `GTIN boş olamaz.`
   - Satır 5: `Pamuk ve elastan oranlarının toplamı 100 olmalıdır.`
4. Panelde üç ürün görünür: Slim Denim 501 (`08690000000012`, Denizli), Regular Denim 502 (`08690000000029`, Denizli) ve Mom Jean 505 (`08690000000050`, Bursa).
5. Slim Denim satırından pasaportu açın. Aynı ürünün üç çıktısı:

- http://localhost:5118/dpp/08690000000012
- http://localhost:5118/dpp/08690000000012.jsonld
- http://localhost:5118/dpp/08690000000012.png

Karbon bu adreslerde görünmez. Yetkili demo görünümü için HTML veya JSON-LD adresinin sonuna `?rol=yetkili` eklenir. Slim Denim için değer `12.4` kg CO2e'dir.

## CSV içe aktarma ve doğrulama

Örnek dosya ve içe aktarma formu şu sütunları bekler:

`gtin`, `model_adi`, `batch_no`, `elyaf_pamuk_yuzde`, `elyaf_elastan_yuzde`, `uretim_yeri`, `karbon_kg_co2e`

`POST /api/import` çok parçalı form verisi alır. Dosya alanı `file` olmalıdır. Dosya yoksa veya zorunlu başlıklardan biri eksikse yanıt `400` olur. Satır hataları `400` değildir; `200` gövdesindeki `hatalar` listesindedir.

Başlık her zaman 1. satırdır. Hata mesajı dosyadaki satır numarasını kullanır. Tamamen boş satırlar atlanır ve sonraki satırın numarası kaymaz. UTF-8 okunur; baştaki BOM yok sayılır. Tırnak içindeki virgül alanın parçasıdır.

Kurallar:

- GTIN boş olamaz. Kırpıldıktan sonra tam 14 hane olmalı, yalnızca `0-9` içermeli ve GS1 kontrol basamağı doğru olmalıdır. Baştaki sıfırlar korunur; GTIN metin olarak işlenir.
- Model adı, parti numarası ve üretim yeri boş olamaz.
- Pamuk ve elastan sayı olmalıdır. Her biri 0 ile 100 arasındadır ve toplamları 100 olmalıdır.
- Karbon sayı olmalıdır ve negatif olamaz.
- Ondalık ayraç noktadır. `12.4` geçerlidir, `12,4` geçersizdir.
- Bir satırdaki hataların tümü birlikte raporlanır. Hatalı satır kaydedilmez; sonraki geçerli satır yazılmaya devam eder.
- Aynı dosyada ikinci kez geçen GTIN kaydedilmez: `Bu GTIN dosyada daha önce geçti`.
- Kayıtlı bir model ve parti, farklı bir üretim yeriyle gelirse kaydedilmez. Partinin üretim yeri bu CSV ile değiştirilemez.

Alanların anlamı:

 Alan | Anlamı |

 --- | --- |

 `basarili` | Yeni eklenen ile `guncellenen` toplamı |

 `guncellenen` | Verisi değiştiği için yeni sürüme geçen satır sayısı |

 `degisiklikYok` | Aynı değerlerle geldiği için yazılmayan satır sayısı |

 `hatali` | Reddedilen satır sayısı |

 `toplamSatir` | `basarili + degisiklikYok + hatali` |

Boş veritabanına ilk yükleme:

```json
{
  "toplamSatir": 5,
  "basarili": 3,
  "hatali": 2,
  "hatalar": [
    { "satir": 4, "nedenler": ["GTIN boş olamaz."] },
    { "satir": 5, "nedenler": ["Pamuk ve elastan oranlarının toplamı 100 olmalıdır."] }
  ],
  "guncellenen": 0,
  "degisiklikYok": 0
}
```

Aynı dosya yeniden yüklenirse üç GTIN çoğalmaz. Satır 4 ve 5 yine hatalıdır:

```json
{
  "toplamSatir": 5,
  "basarili": 0,
  "hatali": 2,
  "hatalar": [
    { "satir": 4, "nedenler": ["GTIN boş olamaz."] },
    { "satir": 5, "nedenler": ["Pamuk ve elastan oranlarının toplamı 100 olmalıdır."] }
  ],
  "guncellenen": 0,
  "degisiklikYok": 3
}
```

Excel'de açıp kaydetmeyin. Excel, GTIN başındaki sıfırları silebilir.

## Veri modeli ve mimari

Üç kayıt birbirine bağlıdır:

- `ProductModel`: model adı. Aynı addan tek kayıt olur.
- `Batch`: bir modele bağlı parti. Parti numarası model içinde tektir. Üretim yeri partiye aittir.
- `Product`: GTIN birincil anahtardır. Pamuk, elastan, karbon, sürüm ve güncelleme zamanı burada tutulur. Ürün bir partiye bağlıdır.

`ProductVersion`, değişen bir ürünün eski hâlini saklar. Güncel kayıt `Product` tablosunda kalır. Eski sürümde model adı, parti, üretim yeri, elyaf, karbon ve o sürümün zamanı durur.

GTIN `string` tutulur. Sayıya çevrilirse baştaki sıfır kaybolur.

Katmanlar:

- `Controllers`: HTTP uçları. İçe aktarma, ürün listesi, sürüm geçmişi, HTML, JSON-LD ve QR.
- `Services`: CSV okuma, satır ve GTIN doğrulama, kayıt kuralları, pasaport görünümü, HTML, JSON-LD, adres ve QR üretimi.
- `Data`: EF Core `DppDbContext` ve benzersizlik kuralları.
- `Models`: `ProductModel`, `Batch`, `Product`, `ProductVersion`.

Şema, uygulama açılışında `EnsureCreated` ile oluşturulur. Migration projesi yoktur.

## Dijital ürün pasaportu

HTML, JSON-LD ve QR aynı ürün kaydından beslenir. Pasaporta giden görünüm `DppView` içindedir. Kayıt yoksa bu uçlar `404` döner. GTIN yolu 14 haneli rakam olmalıdır.

Örnek GTIN `08690000000012`:

 Çıktı | Adres |

 --- | --- |

 HTML | http://localhost:5118/dpp/08690000000012 |

 JSON-LD | http://localhost:5118/dpp/08690000000012.jsonld |

 QR PNG | http://localhost:5118/dpp/08690000000012.png |

JSON-LD belgesi `@context` olarak `https://schema.org`, `@type` olarak `Product` kullanır. Alanlar: `name`, `gtin14`, `url`, `material` ve `additionalProperty`. `gtin14` metindir. `url`, rol parametresi olmayan HTML adresidir. Elyaf `material` ve `additionalProperty` içinde yüzde olarak yer alır. Parti numarası ve üretim yeri de `additionalProperty` içindedir.

QR yanıtının türü `image/png` dir. Kodun içindeki adres, JSON-LD `url` alanıyla aynı tüketici adresidir.

## Tüketici ve yetkili görünümü

Varsayılan görünüm tüketicidir. Karbon ne HTML'de ne JSON-LD'de yazılır.

`?rol=yetkili` karbonu her iki çıktıda da açar. Karşılaştırma büyük/küçük harfe duyarsızdır; `YETKILI` de geçerlidir. `admin`, boş değer veya parametresiz istek karbonu açmaz. Bu bir kimlik doğrulama değildir. Oturum, parola veya yetki kontrolü yoktur.

QR her zaman rolsüz adresi taşır. `?rol=yetkili` QR içine yazılmaz. JSON-LD içindeki `url` de rolsüz kalır.

Sürüm geçmişi de aynı kuralı kullanır:

```bat
curl.exe http://localhost:5118/api/products/08690000000012/versions
```

```bat
curl.exe "http://localhost:5118/api/products/08690000000012/versions?rol=yetkili"
```

Rolsüz yanıtta `karbon` alanı yoktur. `?rol=yetkili` ile alan hem `guncel` hem `eskiSurumler` kayıtlarında bulunur. `GET /api/products` listesi karbon döndürmez. Yönetim panelindeki ürün listesi ve ürün detayı da karbon değerini göstermez.

## Bonus: Sürümleme

Aynı GTIN aynı model, parti, üretim yeri, elyaf ve karbonla yeniden gelirse kayıt yazılmaz ve yeni sürüm oluşmaz.

Aynı GTIN'in bu alanlarından biri değişirse güncel ürünün sürümü artar. Değişmeden önceki hâl `ProductVersion` tablosuna, kendi sürüm numarası ve zamanıyla yazılır. Güncel sürüm `Product` üzerinde kalır.

Bunu örnek dosyanın kendisi yapmaz. Deneme veritabanında dosyanın bir kopyasını alın ve yalnızca `08690000000012` satırında pamuğu `97`, elastanı `3` yapın. Toplam yine 100 olur; karbon `12.4` olarak kalabilir. Beklenen içe aktarma sonucu:

```json
{
  "toplamSatir": 5,
  "basarili": 1,
  "hatali": 2,
  "guncellenen": 1,
  "degisiklikYok": 2
}
```

`GET /api/products/08690000000012/versions` ardından güncel sürüm 2, pamuk `97`, elastan `3` olur. `eskiSurumler` içinde sürüm 1, pamuk `98`, elastan `2` durur. `updatedAt` içe aktarma anının UTC zamanıdır; sabit bir tarih değildir. `hatalar` dizisi ilk yüklemedeki satır 4 ve satır 5 kayıtlarını içerir.

Aynı GTIN iki kez güncellenirse iki eski sürüm saklanır.

## Yönetim paneli

Panel `MiniDpp.Api/wwwroot/index.html` dosyasıdır ve http://localhost:5118 adresinden sunulur. Sunucu metinleri sayfaya metin olarak yazılır.

Sol menü:

- **Ana Panel:** özet sayılar, son CSV sonucu, veri kalitesi, pasaport önizlemesi, üretim haritası ve son hareketler.
- **Ürün Yönetimi:** arama, model, parti, üretim yeri ve doğrulama filtreleri. Satır ürün detayını açar. Karbon bu listede yoktur.
- **CSV İçe Aktarma:** dosya seçme veya sürükleme, doğrulama özeti ve hatalı satırlar.
- **Raporlar ve Veri Kalitesi:** içe aktarma geçmişi, doğrulama, üretim dağılımı ve pasaport bağlantıları.
- **Ayarlar:** tema, tablo yoğunluğu ve tarih biçimi. Tercihler bu tarayıcıda saklanır; ürün verisini ve pasaport içeriğini değiştirmez.
- **Yardım ve Hakkında:** kullanım rehberi ve sık sorulan sorular.

**Dışa aktar**, kayıtlı ürünlerden `mini-dpp-envanter.csv` indirir. Sütunlar `gtin`, `model_adi`, `batch_no`, `uretim_yeri`, `elyaf_pamuk_yuzde`, `elyaf_elastan_yuzde`, `surum` ve `guncelleme` dir. Karbon bu dosyada yoktur.

Üretim haritası, veritabanındaki üretim yerlerine işaret koyar. Türkiye'nin 81 ili için koordinat vardır. Ürünü olmayan il çizilmez. Eşleştirme; boşluk, büyük/küçük harf ve Türkçe karakter farkını yalnızca haritada birleştirir. Kayıtlı şehir adı değiştirilmez. Tanınmayan yer listede kalır ve harita altında belirtilir. Bir ile basınca ürün listesi o yerle süzülür.

Son CSV özeti tarayıcıda `mini-dpp-son-aktarim` anahtarında tutulur. Kayıt, içe aktarma anındaki ürünlerin GTIN, sürüm ve güncelleme zamanından oluşan bir damgayla karşılaştırılır. Damga yoksa veya güncel ürünlerle uyuşmuyorsa eski özet gösterilmez ve anahtar silinir. Sonuç yokken kartta yalnızca şu metin vardır:

> Henüz CSV içe aktarma işlemi yapılmadı.

Yeni bir CSV yüklendiğinde özet yeniden yazılır ve damga güncellenir. Aynı kayıt, CSV ekranındaki son sonuç ile raporlardaki içe aktarma geçmişini de besler.

## Testler

Repo kökünde:

```bash
dotnet test
```

`MiniDpp.Tests` xUnit projesidir. Testler bellek içi SQLite kullanır; `MiniDpp.Api/mini-dpp.db` dosyasını açmaz.

Doğrulanan gruplar: GTIN ve satır doğrulama, CSV içe aktarma, HTML, JSON-LD, QR, rol görünürlüğü, ürün listesi ve sürümleme.

`dotnet test` komutuyla 54 otomatik test başarıyla tamamlanmıştır (0 başarısız, 0 atlanan). Testler bellek içi SQLite kullanır ve uygulamanın yerel veritabanından bağımsızdır.

## Proje yapısı

```text
MiniDpp.sln
README.md
ornek/
  urunler_duzeltilmis_test.csv
MiniDpp.Api/
  Program.cs
  appsettings.json
  Controllers/
  Data/
  Models/
  Services/
  wwwroot/
    index.html
    banner-desen.svg
MiniDpp.Tests/
```

## Tasarım kararları ve sınırlar

- SQLite tek dosyalık yerel bir veritabanıdır. Ayrı bir sunucu kurulmaz. Dosya, sürecin çalışma dizinine görelidir.
- GTIN metindir. Böylece 14 hanenin başındaki sıfır korunur.
- Model, parti ve ürün ayrı tablolardadır. Üretim yeri partiye aittir. Aynı model ve parti yeniden geldiğinde mevcut parti kullanılır.
- Elyaf ve karbon `decimal` dir. EF Core SQLite sağlayıcısı bu değerleri metin olarak saklar. Pamuk ve elastan için kesinlik `(5, 2)` dir.
- Hatalı satır, aynı dosyadaki geçerli satırların kaydını durdurmaz. Her başarılı satır ayrı kaydedilir. Çok büyük bir CSV bu yüzden yavaşlayabilir.
- `EnsureCreated` şemayı yalnızca yoksa oluşturur. Migration yoktur. Model değişirse mevcut `mini-dpp.db` yeni şemaya kendiliğinden uymaz; dosyanın kaldırılması gerekir.
- `?rol=yetkili` yalnızca demodur. Gerçek kimlik doğrulama yoktur.
- `appsettings.json` içindeki `PublicBaseUrl` boştur. Boşken QR ve JSON-LD adresi istekten üretilir. Doluysa bu iki çıktı o tabanı kullanır ve yine rol parametresi eklemez. Telefonla okutmak için buraya bilgisayarın erişilen adresi yazılabilir.
- Pasaport uçları tam 14 haneli GTIN ister. Yönetim paneli ayrı bir çatı veya derleme adımı kullanmaz.

## Teknik değerlendirmeye uygunluk

- CSV doğrulama ve kısmi kayıt: GTIN, zorunlu alan, elyaf ve karbon kuralları uygulanır. Hatalı satır reddedilir, geçerli satır yazılır.
- Saklama: model, parti ve ürün SQLite'a EF Core ile yazılır. GTIN metin birincil anahtardır.
- Pasaport: aynı kaynaktan HTML, schema.org/Product JSON-LD ve QR PNG üretilir.
- Görünürlük: karbon varsayılan çıktıda yoktur. `?rol=yetkili` yalnızca demo amaçlıdır.
- Bonus: değişmeyen GTIN yeni sürüm açmaz. Değişen GTIN'in eski hâli `ProductVersion` tablosunda kalır ve sürüm ucuyla okunur.
- Testler bu kuralları bellek içi veritabanında doğrular.

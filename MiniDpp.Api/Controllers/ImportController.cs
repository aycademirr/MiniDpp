// CSV yükleme uç noktası. Eksik başlık 400 döner; satır hataları sonuç gövdesindedir.

using Microsoft.AspNetCore.Mvc;
using MiniDpp.Api.Services;

namespace MiniDpp.Api.Controllers;

[ApiController]
[Route("api/import")]
public sealed class ImportController : ControllerBase
{
    private readonly CsvImportService _importService;

    public ImportController(CsvImportService importService)
    {
        _importService = importService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new { mesaj = "CSV dosyası 'file' alanında gönderilmelidir." });

        await using var stream = file.OpenReadStream();
        var sonuc = await _importService.ImportAsync(stream, cancellationToken);
        if (sonuc.EksikBasliklar is not null)
        {
            return BadRequest(new
            {
                mesaj = "Eksik CSV başlıkları: " + string.Join(", ", sonuc.EksikBasliklar) + ".",
                eksikBasliklar = sonuc.EksikBasliklar
            });
        }

        return Ok(sonuc.Rapor);
    }
}

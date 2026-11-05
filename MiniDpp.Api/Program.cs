// API başlangıcı: denetleyiciler, statik ön yüz, SQLite bağlantısı ve veritabanının oluşturulması.

using Microsoft.EntityFrameworkCore;
using MiniDpp.Api.Data;
using MiniDpp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<CsvImportService>();

builder.Services.AddDbContext<DppDbContext>(options =>
    options.UseSqlite("Data Source=mini-dpp.db"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DppDbContext>();
    db.Database.EnsureCreated();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Ürün modeli tanımı; aynı isimden yalnızca bir kayıt olabilir.

namespace MiniDpp.Api.Models;

public class ProductModel
{
    public int Id { get; set; }

    public required string Name { get; set; }
}

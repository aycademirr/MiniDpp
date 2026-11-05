// Üretim partisi; bir modele bağlıdır ve parti numarası model içinde tektir.

namespace MiniDpp.Api.Models;

public class Batch
{
    public int Id { get; set; }

    public required string BatchNo { get; set; }

    public required string ProductionPlace { get; set; }

    public int ModelId { get; set; }

    public ProductModel Model { get; set; } = null!;
}

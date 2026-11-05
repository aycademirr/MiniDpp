// DPP veritabanı bağlamı: tablo eşlemesi ve benzersizlik kuralları.

using Microsoft.EntityFrameworkCore;
using MiniDpp.Api.Models;

namespace MiniDpp.Api.Data;

public class DppDbContext : DbContext
{
    public DppDbContext(DbContextOptions<DppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProductModel> ProductModels => Set<ProductModel>();

    public DbSet<Batch> Batches => Set<Batch>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductVersion> ProductVersions => Set<ProductVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductModel>()
            .HasIndex(model => model.Name)
            .IsUnique();

        modelBuilder.Entity<Batch>()
            .HasIndex(batch => new { batch.ModelId, batch.BatchNo })
            .IsUnique();

        modelBuilder.Entity<Product>(product =>
        {
            product.HasKey(item => item.Gtin);
            product.Property(item => item.Gtin).HasMaxLength(14);
            product.Property(item => item.CottonPercent).HasPrecision(5, 2);
            product.Property(item => item.ElastanPercent).HasPrecision(5, 2);
        });

        modelBuilder.Entity<ProductVersion>(version =>
        {
            version.HasIndex(item => new { item.Gtin, item.Version }).IsUnique();
            version.Property(item => item.Gtin).HasMaxLength(14);
            version.Property(item => item.CottonPercent).HasPrecision(5, 2);
            version.Property(item => item.ElastanPercent).HasPrecision(5, 2);
            version.HasOne<Product>()
                .WithMany()
                .HasForeignKey(item => item.Gtin)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

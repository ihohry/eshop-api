using EShop.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EShop.Infrastructure.Catalog;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_Products_Price_Positive", "\"Price\" > 0"));

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(CatalogDefaults.NameMaxLength).IsRequired();
        builder.Property(p => p.Slug).HasMaxLength(CatalogDefaults.SlugMaxLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(CatalogDefaults.DescriptionMaxLength);
        builder.Property(p => p.Price).HasPrecision(18, 2);

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => new { p.IsActive, p.CategoryId });

        // A category with products cannot be deleted.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
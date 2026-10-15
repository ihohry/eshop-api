using EShop.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EShop.Infrastructure.Catalog;

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_ProductImages_SizeBytes_Positive", "\"SizeBytes\" > 0"));

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.FileName).HasMaxLength(100).IsRequired();
        builder.Property(i => i.ContentType).HasMaxLength(50).IsRequired();
        builder.Property(i => i.AltText).HasMaxLength(ImageDefaults.AltTextMaxLength);

        builder.HasIndex(i => i.FileName).IsUnique();
        builder.HasIndex(i => new { i.ProductId, i.Position });

        // Products are never hard-deleted (soft delete), so no cascade: the files must not be orphaned.
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
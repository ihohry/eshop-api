using EShop.Domain.Orders;
using EShop.Domain.Returns;
using EShop.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EShop.Infrastructure.Returns;

public sealed class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> builder)
    {
        builder.ToTable("ReturnRequests", t =>
            t.HasCheckConstraint("CK_ReturnRequests_Refund_Positive", "\"RefundAmount\" > 0"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Reason).HasMaxLength(ReturnRequest.MaxReasonLength).IsRequired();
        builder.Property(r => r.RefundAmount).HasPrecision(18, 2);

        builder.HasOne<Order>().WithMany()
            .HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Items).WithOne().HasForeignKey(i => i.ReturnRequestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(r => r.OrderId);
        builder.HasIndex(r => new { r.UserId, r.CreatedAt });
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
    }
}

public sealed class ReturnItemConfiguration : IEntityTypeConfiguration<ReturnItem>
{
    public void Configure(EntityTypeBuilder<ReturnItem> builder)
    {
        builder.ToTable("ReturnItems", t =>
            t.HasCheckConstraint("CK_ReturnItems_Quantity_Positive", "\"Quantity\" > 0"));
        builder.HasKey(i => new { i.ReturnRequestId, i.ProductId });
        builder.HasOne<EShop.Domain.Catalog.Product>().WithMany()
            .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
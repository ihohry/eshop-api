using EShop.Domain.Orders;
using EShop.Domain.Payments;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EShop.Infrastructure.Payments;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", t =>
        {
            t.HasCheckConstraint("CK_Payments_Amount_Positive", "\"Amount\" > 0");
            t.HasCheckConstraint("CK_Payments_Status_PaidAt",
                "(\"Status\" IN ('Paid', 'Refunded') AND \"PaidAt\" IS NOT NULL) " +
                "OR (\"Status\" IN ('Pending', 'Expired') AND \"PaidAt\" IS NULL)");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Provider).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.ProviderSessionId).HasMaxLength(255).IsRequired();
        builder.Property(p => p.ProviderPaymentReference).HasMaxLength(255);
        builder.Property(p => p.Url).HasMaxLength(2048).IsRequired();
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();

        // Payments of an order are kept for the history, never deleted by accident.
        builder.HasOne<Order>().WithMany()
            .HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Restrict);

        // The webhook finds the payment by the provider session id.
        builder.HasIndex(p => new { p.Provider, p.ProviderSessionId }).IsUnique();

        // An order can have many payments over time, but only one open (Pending) at a time.
        builder.HasIndex(p => p.OrderId, "UX_Payments_OrderId_Pending")
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");

        // Lookup of all payments of an order (also covers the foreign key).
        builder.HasIndex(p => p.OrderId);
    }
}
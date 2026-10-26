using EShop.Domain.Orders;
using EShop.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EShop.Infrastructure.Orders;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_Total_Positive", "\"Total\" > 0");
            t.HasCheckConstraint("CK_Orders_Delivery_Fields",
                "(\"DeliveryMethod\" = 'Courier' AND \"PickupPointId\" IS NULL AND \"PickupPointName\" IS NULL) " +
                "OR (\"DeliveryMethod\" = 'PickupPoint' AND \"PickupPointId\" IS NOT NULL AND \"PickupPointName\" IS NOT NULL)");
            t.HasCheckConstraint("CK_Orders_Courier_Prepaid",
                "\"DeliveryMethod\" = 'PickupPoint' OR \"PaymentMethod\" = 'Prepaid'");
            t.HasCheckConstraint("CK_Orders_Status_PaymentMethod",
                "\"PaymentMethod\" = 'PayOnReceipt' OR \"Status\" NOT IN ('PendingConfirmation', 'Confirmed', 'Refused')");
        });
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Currency).HasMaxLength(3);
        builder.Property(o => o.Total).HasPrecision(18, 2);
        builder.Property(o => o.DeliveryMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(PaymentMethod.Prepaid) // existing rows become Prepaid
            .HasSentinel(PaymentMethod.Prepaid);
        builder.Property(o => o.PickupPointId).HasMaxLength(OrderDefaults.MaxPickupPointIdLength);
        builder.Property(o => o.PickupPointName).HasMaxLength(OrderDefaults.MaxTextLength);

        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(o => o.ShippingAddress, a =>
        {
            a.Property(x => x.FullName).HasMaxLength(OrderDefaults.MaxTextLength).IsRequired();
            a.Property(x => x.Line1).HasMaxLength(OrderDefaults.MaxTextLength).IsRequired();
            a.Property(x => x.Line2).HasMaxLength(OrderDefaults.MaxTextLength);
            a.Property(x => x.PostalCode).HasMaxLength(20).IsRequired();
            a.Property(x => x.City).HasMaxLength(OrderDefaults.MaxTextLength).IsRequired();
            a.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength().IsRequired();
            a.Property(x => x.Phone).HasMaxLength(OrderDefaults.MaxPhoneLength).IsRequired();
        });

        builder.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(o => new { o.UserId, o.CreatedAt });
        builder.HasIndex(o => new { o.Status, o.ExpiresAt }); // for the expiry job (T4.5)
    }
}
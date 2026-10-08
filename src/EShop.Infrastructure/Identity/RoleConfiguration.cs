using EShop.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EShop.Infrastructure.Identity;

public sealed class RoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public static readonly Guid CustomerRoleId = Guid.Parse("43b3f28c-a5bb-4634-8d60-0f83e058778a");
    public static readonly Guid AdminRoleId = Guid.Parse("c5997bde-f504-4fb9-9306-4082a0552c84");

    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.HasData(
            CreateRole(CustomerRoleId, Roles.Customer),
            CreateRole(AdminRoleId, Roles.Admin));
    }

    private static IdentityRole<Guid> CreateRole(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        // A fixed value, so the migration does not change on every run.
        ConcurrencyStamp = id.ToString(),
    };
}
using System.ComponentModel.DataAnnotations;
using EShop.Domain.Catalog;

namespace EShop.Api.Catalog;

internal static class SlugRule
{
    public const string Pattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class MaxTwoDecimalsAttribute()
    : ValidationAttribute("The field {0} must have at most 2 decimals.")
{
    public override bool IsValid(object? value) => value is not decimal d || decimal.Round(d, 2) == d;
}

public sealed record CategoryCreateRequest(
    [Required, StringLength(CatalogDefaults.NameMaxLength)] string Name,
    [Required, StringLength(CatalogDefaults.SlugMaxLength), RegularExpression(SlugRule.Pattern)] string Slug,
    Guid? ParentId);

public sealed record CategoryUpdateRequest(
    [Required, StringLength(CatalogDefaults.NameMaxLength)] string Name,
    [Required, StringLength(CatalogDefaults.SlugMaxLength), RegularExpression(SlugRule.Pattern)] string Slug);

public sealed record ProductCreateRequest(
    Guid CategoryId,
    [Required, StringLength(CatalogDefaults.NameMaxLength)] string Name,
    [Required, StringLength(CatalogDefaults.SlugMaxLength), RegularExpression(SlugRule.Pattern)] string Slug,
    [StringLength(CatalogDefaults.DescriptionMaxLength)] string? Description,
    [Range(0.01, 1_000_000_000), MaxTwoDecimals] decimal Price);

public sealed record ProductUpdateRequest(
    Guid CategoryId,
    [Required, StringLength(CatalogDefaults.NameMaxLength)] string Name,
    [Required, StringLength(CatalogDefaults.SlugMaxLength), RegularExpression(SlugRule.Pattern)] string Slug,
    [StringLength(CatalogDefaults.DescriptionMaxLength)] string? Description,
    [Range(0.01, 1_000_000_000), MaxTwoDecimals] decimal Price,
    bool IsActive);

public sealed record AdminCategoryResponse(Guid Id, string Name, string Slug, Guid? ParentId);

public sealed record AdminProductResponse(
    Guid Id, Guid CategoryId, string Name, string Slug, string? Description,
    decimal Price, string Currency, bool IsActive);
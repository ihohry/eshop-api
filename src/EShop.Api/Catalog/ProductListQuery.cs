using System.ComponentModel.DataAnnotations;

namespace EShop.Api.Catalog;

public enum ProductSort
{
    Name,
    PriceAsc,
    PriceDesc,
    Newest,
}

public sealed record ProductListQuery(
    [Range(1, 100_000)] int Page = 1,
    [Range(1, 100)] int PageSize = 20,
    [StringLength(100)] string? Search = null,
    [StringLength(200)] string? Category = null,
    ProductSort Sort = ProductSort.Name);
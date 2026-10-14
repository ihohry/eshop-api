using System.ComponentModel.DataAnnotations;

namespace EShop.Infrastructure.Images;

public sealed class ImageStorageOptions
{
    public const string SectionName = "ImageStorage";

    [Required]
    public string RootPath { get; init; } = "data/images";

    [Range(1024, 20 * 1024 * 1024)]
    public long MaxFileSizeBytes { get; init; } = 5 * 1024 * 1024;
}
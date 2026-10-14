using System.Text.RegularExpressions;

namespace EShop.Domain.Catalog;

internal static partial class CatalogValidation
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    public static string Name(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        var name = value.Trim();
        return name.Length > CatalogDefaults.NameMaxLength
            ? throw new ArgumentException($"Name is longer than {CatalogDefaults.NameMaxLength} characters.", paramName)
            : name;
    }

    public static string Slug(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Length <= CatalogDefaults.SlugMaxLength && SlugPattern().IsMatch(value)
            ? value
            : throw new ArgumentException("Slug must be lowercase letters, digits and single hyphens.", paramName);
    }

    public static decimal Price(decimal value, string paramName)
    {
        return value > 0 && decimal.Round(value, 2) == value
            ? value
            : throw new ArgumentOutOfRangeException(paramName, value, "Price must be positive with at most 2 decimals.");
    }
}
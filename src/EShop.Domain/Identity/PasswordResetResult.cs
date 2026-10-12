namespace EShop.Domain.Identity;

public sealed record PasswordResetResult(
    bool Succeeded, bool InvalidToken, IReadOnlyDictionary<string, string[]> Errors)
{
    public static PasswordResetResult Success() =>
        new(true, false, new Dictionary<string, string[]>(StringComparer.Ordinal));

    public static PasswordResetResult InvalidLink() =>
        new(false, true, new Dictionary<string, string[]>(StringComparer.Ordinal));

    public static PasswordResetResult Failure(IReadOnlyDictionary<string, string[]> errors) =>
        new(false, false, errors);
}
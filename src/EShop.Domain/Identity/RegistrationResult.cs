namespace EShop.Domain.Identity;

public sealed class RegistrationResult
{
    private static readonly Dictionary<string, string[]> NoErrors = [];

    private RegistrationResult(Guid? userId, IReadOnlyDictionary<string, string[]> errors)
    {
        UserId = userId;
        Errors = errors;
    }

    public Guid? UserId { get; }

    /// <summary>Validation errors by field name. Empty on success.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public bool Succeeded => UserId is not null;

    public static RegistrationResult Success(Guid userId) => new(userId, NoErrors);

    public static RegistrationResult Failure(IReadOnlyDictionary<string, string[]> errors) => new(null, errors);
}
using EShop.Domain.Exceptions;
using EShop.Domain.Identity;
using EShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EShop.Infrastructure.Identity;

public sealed partial class AccountService(
    UserManager<ApplicationUser> userManager,
    AppDbContext dbContext,
    ILogger<AccountService> logger) : IAccountService
{
    public async Task<RegistrationResult> RegisterCustomerAsync(string email, string password)
    {
        var trimmedEmail = email.Trim();
        var user = new ApplicationUser { UserName = trimmedEmail, Email = trimmedEmail };

        // One transaction, so a user is never saved without its role.
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            return ToFailure(created);
        }

        var roleAdded = await userManager.AddToRoleAsync(user, Roles.Customer);
        if (!roleAdded.Succeeded)
        {
            // The role is seeded by a migration, so this is a system error, not bad input.
            var codes = string.Join(", ", roleAdded.Errors.Select(error => error.Code));
            throw new InvalidOperationException($"Could not assign the Customer role: {codes}");
        }

        await transaction.CommitAsync();

        LogCustomerRegistered(logger, user.Id);
        return RegistrationResult.Success(user.Id);
    }

    private static RegistrationResult ToFailure(IdentityResult result)
    {
        var isDuplicate = result.Errors.Any(error =>
            error.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
                or nameof(IdentityErrorDescriber.DuplicateUserName));

        if (isDuplicate)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var errors = result.Errors
            .GroupBy(error => FieldFor(error.Code), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray(),
                StringComparer.Ordinal);

        return RegistrationResult.Failure(errors);
    }

    private static string FieldFor(string code) => code switch
    {
        _ when code.StartsWith("Password", StringComparison.Ordinal) => "Password",
        nameof(IdentityErrorDescriber.InvalidEmail) or nameof(IdentityErrorDescriber.InvalidUserName) => "Email",
        _ => "Registration",
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Customer registered: {UserId}")]
    private static partial void LogCustomerRegistered(ILogger logger, Guid userId);
}
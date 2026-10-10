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
    
    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return LoginResult.InvalidCredentials();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            LogLoginRejectedLockedOut(logger, user.Id);
            return LoginResult.LockedOut();
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            // Counts the failure and locks the account when the limit is reached.
            await userManager.AccessFailedAsync(user);

            if (await userManager.IsLockedOutAsync(user))
            {
                LogAccountLockedOut(logger, user.Id);
                return LoginResult.LockedOut();
            }

            return LoginResult.InvalidCredentials();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        LogLoginSucceeded(logger, user.Id);

        return LoginResult.Success(new AuthenticatedUser(user.Id, user.Email!, roles.ToArray()));
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Login succeeded: {UserId}")]
    private static partial void LogLoginSucceeded(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Account locked out after too many failed logins: {UserId}")]
    private static partial void LogAccountLockedOut(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login rejected, account is locked out: {UserId}")]
    private static partial void LogLoginRejectedLockedOut(ILogger logger, Guid userId);
}
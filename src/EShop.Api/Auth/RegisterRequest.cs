using System.ComponentModel.DataAnnotations;

namespace EShop.Api.Auth;

public sealed record RegisterRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Password);

public sealed record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, string RefreshToken);
public sealed record RefreshTokenRequest([property: Required] string RefreshToken);
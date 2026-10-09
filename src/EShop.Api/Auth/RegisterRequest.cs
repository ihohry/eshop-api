using System.ComponentModel.DataAnnotations;

namespace EShop.Api.Auth;

public sealed record RegisterRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Password);
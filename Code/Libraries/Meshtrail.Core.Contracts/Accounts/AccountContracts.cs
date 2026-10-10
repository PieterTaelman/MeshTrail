namespace Meshtrail.Core.Contracts.Accounts;

/// <summary>Body of POST account/register. Password: at least 10 characters.</summary>
public sealed record RegisterRequest(string Email, string FirstName, string LastName, string Password);

/// <summary>Body of POST account/confirm-email: the two values from the link in the confirmation mail.</summary>
public sealed record ConfirmEmailRequest(Guid UserId, string Token);

/// <summary>Body of POST account/resend-confirmation and account/forgot-password.</summary>
public sealed record EmailRequest(string Email);

public sealed record SignInRequest(string Email, string Password);

/// <summary>Body of POST account/reset-password: the values from the reset mail and the new password.</summary>
public sealed record ResetPasswordRequest(Guid UserId, string Token, string Password);

public sealed record UpdateProfileRequest(string FirstName, string LastName);

/// <summary>Your account as you see it.</summary>
public sealed record ProfileDto(Guid Id, string Email, string FirstName, string LastName, string DisplayName, DateTimeOffset CreatedAt);

/// <summary>
/// Answer of POST account/sign-in: send AccessToken as "Authorization: Bearer …" (SignalR: ?access_token=…)
/// until ExpiresAt, then sign in again.
/// </summary>
public sealed record SignInResponse(string AccessToken, DateTimeOffset ExpiresAt, ProfileDto Profile);

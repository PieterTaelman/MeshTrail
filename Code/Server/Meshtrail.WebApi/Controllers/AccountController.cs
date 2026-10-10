using Asp.Versioning;
using Mediator;
using Meshtrail.Core.Application.UseCases.Accounts.Commands.ConfirmEmail;
using Meshtrail.Core.Application.UseCases.Accounts.Commands.Register;
using Meshtrail.Core.Application.UseCases.Accounts.Commands.RequestPasswordReset;
using Meshtrail.Core.Application.UseCases.Accounts.Commands.ResendConfirmation;
using Meshtrail.Core.Application.UseCases.Accounts.Commands.ResetPassword;
using Meshtrail.Core.Application.UseCases.Accounts.Commands.SignIn;
using Meshtrail.Core.Application.UseCases.Accounts.Commands.UpdateProfile;
using Meshtrail.Core.Application.UseCases.Accounts.Queries.GetMyProfile;
using Meshtrail.Core.Contracts.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Meshtrail.WebApi.Infrastructure;

namespace Meshtrail.WebApi.Controllers;

/// <summary>
/// Meshtrail accounts: register, confirm the email address, sign in, reset a forgotten password, your profile.
/// Rate-limited per client address. Register / resend / forgot always answer 202, whether the address exists or not.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/account")]
[EnableRateLimiting(RateLimits.Account)]
public sealed class AccountController(ISender sender) : ControllerBase
{
    /// <summary>Registers and mails a confirmation link (202).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new RegisterCommand(request.Email, request.FirstName, request.LastName, request.Password), cancellationToken);
        return Accepted();
    }

    /// <summary>Confirms the email address with the values from the link (422 when invalid or expired).</summary>
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new ConfirmEmailCommand(request.UserId, request.Token), cancellationToken);
        return NoContent();
    }

    [HttpPost("resend-confirmation")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendConfirmation(EmailRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new ResendConfirmationCommand(request.Email), cancellationToken);
        return Accepted();
    }

    /// <summary>Returns an access token. 401 wrong email or password, 403 email not confirmed, 429 locked for a while.</summary>
    [HttpPost("sign-in")]
    [AllowAnonymous]
    public async Task<ActionResult<SignInResponse>> SignIn(SignInRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new SignInCommand(request.Email, request.Password), cancellationToken));

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new RequestPasswordResetCommand(request.Email), cancellationToken);
        return Accepted();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new ResetPasswordCommand(request.UserId, request.Token, request.Password), cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<ProfileDto>> GetMe(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMyProfileQuery(), cancellationToken));

    [HttpPut("me")]
    public async Task<ActionResult<ProfileDto>> UpdateMe(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdateProfileCommand(request.FirstName, request.LastName), cancellationToken));
}

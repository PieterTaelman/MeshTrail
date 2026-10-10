using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Meshtrail.Core.Contracts.Accounts;
using Meshtrail.Core.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace Meshtrail.Core.IntegrationTests.Accounts;

/// <summary>Register → confirm by mail link → sign in → use the token; and the things that must not work.</summary>
[TestClass]
public sealed class AccountApiTests
{
    private const string AccountUrl = "/api/v1/account";
    private const string Password = "correct horse battery";

    private static CapturingEmailSender Mail => AssemblySetup.Factory.Mail;

    [TestMethod]
    public async Task RegisterConfirmSignIn_TokenWorksForTheProfile()
    {
        // Arrange
        using var client = AssemblySetup.Factory.CreateClient();
        var email = UniqueEmail();

        // Act
        var register = await client.PostAsJsonAsync($"{AccountUrl}/register", new RegisterRequest(email, "Ann", "Peak", Password));
        var (userId, token) = Mail.LinkFor(email, "/account/confirm");
        var confirm = await client.PostAsJsonAsync($"{AccountUrl}/confirm-email", new ConfirmEmailRequest(userId, token));
        var signIn = await client.PostAsJsonAsync($"{AccountUrl}/sign-in", new SignInRequest(email.ToUpperInvariant(), Password));
        var session = (await signIn.Content.ReadFromJsonAsync<SignInResponse>())!;
        using var signedIn = AssemblySetup.Factory.CreateClient();
        signedIn.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var me = await signedIn.GetFromJsonAsync<ProfileDto>($"{AccountUrl}/me");

        // Assert
        register.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        confirm.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        me!.Id.ShouldBe(userId);
        me.DisplayName.ShouldBe("Ann Peak");
        me.Email.ShouldBe(email);
    }

    [TestMethod]
    public async Task SignIn_BeforeConfirming_Returns403()
    {
        // Arrange
        using var client = AssemblySetup.Factory.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync($"{AccountUrl}/register", new RegisterRequest(email, "Ann", "Peak", Password));

        // Act
        var signIn = await client.PostAsJsonAsync($"{AccountUrl}/sign-in", new SignInRequest(email, Password));

        // Assert
        signIn.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task SignIn_WrongPassword_Returns401()
    {
        // Arrange
        using var client = AssemblySetup.Factory.CreateClient();
        var email = await RegisterConfirmedAsync(client);

        // Act
        var signIn = await client.PostAsJsonAsync($"{AccountUrl}/sign-in", new SignInRequest(email, "wrong password!"));

        // Assert
        signIn.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await signIn.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.ShouldBe("Wrong email or password.");
    }

    [TestMethod]
    public async Task Register_ShortPassword_Returns400()
    {
        // Act
        using var client = AssemblySetup.Factory.CreateClient();
        var response = await client.PostAsJsonAsync($"{AccountUrl}/register", new RegisterRequest(UniqueEmail(), "Ann", "Peak", "short"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys.ShouldContain("Password");
    }

    [TestMethod]
    public async Task Register_ExistingAccount_SameAnswerAndAHintMail()
    {
        // Arrange
        using var client = AssemblySetup.Factory.CreateClient();
        var email = await RegisterConfirmedAsync(client);

        // Act
        var again = await client.PostAsJsonAsync($"{AccountUrl}/register", new RegisterRequest(email, "Eve", "Other", "another password"));

        // Assert
        again.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Mail.To(email).Last().Subject.ShouldContain("already have");
    }

    [TestMethod]
    public async Task ForgotPassword_ResetLinkSetsANewPassword()
    {
        // Arrange
        using var client = AssemblySetup.Factory.CreateClient();
        var email = await RegisterConfirmedAsync(client);

        // Act
        await client.PostAsJsonAsync($"{AccountUrl}/forgot-password", new EmailRequest(email));
        var (userId, token) = Mail.LinkFor(email, "/account/reset");
        var reset = await client.PostAsJsonAsync($"{AccountUrl}/reset-password", new ResetPasswordRequest(userId, token, "a brand new password"));
        var signIn = await client.PostAsJsonAsync($"{AccountUrl}/sign-in", new SignInRequest(email, "a brand new password"));

        // Assert
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task Me_WithoutToken_Returns401()
    {
        // Arrange
        using var client = AssemblySetup.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "forged.token.value");

        // Act
        var response = await client.GetAsync($"{AccountUrl}/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string UniqueEmail() => $"t{Guid.NewGuid():N}@example.org";

    private static async Task<string> RegisterConfirmedAsync(HttpClient client)
    {
        var email = UniqueEmail();
        await client.PostAsJsonAsync($"{AccountUrl}/register", new RegisterRequest(email, "Ann", "Peak", Password));
        var (userId, token) = Mail.LinkFor(email, "/account/confirm");
        (await client.PostAsJsonAsync($"{AccountUrl}/confirm-email", new ConfirmEmailRequest(userId, token))).EnsureSuccessStatusCode();
        return email;
    }
}

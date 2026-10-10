using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Accounts.Commands.ConfirmEmail;
using Meshtrail.Core.Application.UseCases.Accounts.Commands.Register;
using Meshtrail.Core.Application.UseCases.Accounts.Commands.SignIn;
using Meshtrail.Core.Domain.Accounts;
using Meshtrail.Core.Domain.Common;
using Moq;
using Shouldly;
using static Meshtrail.Core.UnitTests.Accounts.AccountTestHelpers;

namespace Meshtrail.Core.UnitTests.Accounts;

[TestClass]
public sealed class AccountTests
{
    [TestMethod]
    [DataRow("not-an-email")]
    [DataRow("")]
    [DataRow("a@b@c")]
    public void Register_InvalidEmail_Throws(string email)
    {
        // Act + Assert
        Should.Throw<DomainException>(() => UserAccount.Register(email, "Ann", "Peak", "hash", Token, Now));
    }

    [TestMethod]
    public void Register_Email_IsStoredLowerCaseAndUnconfirmed()
    {
        // Act
        var account = NewAccount("  Ann.Peak@Example.ORG ");

        // Assert
        account.Email.ShouldBe("ann.peak@example.org");
        account.IsConfirmed.ShouldBeFalse();
        account.DisplayName.ShouldBe("Ann Peak");
    }

    [TestMethod]
    public void ConfirmEmail_RightToken_Confirms()
    {
        // Arrange
        var account = NewAccount();

        // Act + Assert
        account.ConfirmEmail(Token, Now.AddHours(1)).ShouldBeTrue();
        account.IsConfirmed.ShouldBeTrue();
    }

    [TestMethod]
    public void ConfirmEmail_WrongToken_DoesNotConfirm()
    {
        // Arrange
        var account = NewAccount();

        // Act + Assert
        account.ConfirmEmail("wrong", Now).ShouldBeFalse();
        account.IsConfirmed.ShouldBeFalse();
    }

    [TestMethod]
    public void ConfirmEmail_Expired_DoesNotConfirm()
    {
        // Arrange
        var account = NewAccount();

        // Act + Assert
        account.ConfirmEmail(Token, Now + UserAccount.ConfirmationLifetime + TimeSpan.FromMinutes(1)).ShouldBeFalse();
    }

    [TestMethod]
    public void SignIn_NotConfirmed_IsRefusedEvenWithTheRightPassword()
    {
        // Act + Assert
        NewAccount().SignIn(passwordOk: true, Now).ShouldBe(SignInResult.NotConfirmed);
    }

    [TestMethod]
    public void SignIn_FiveWrongPasswords_LocksTheAccount()
    {
        // Arrange
        var account = ConfirmedAccount();

        // Act
        for (var i = 0; i < UserAccount.MaxFailedSignIns - 1; i++)
        {
            account.SignIn(passwordOk: false, Now).ShouldBe(SignInResult.WrongPassword);
        }

        var fifth = account.SignIn(passwordOk: false, Now);
        var rightButLocked = account.SignIn(passwordOk: true, Now.AddMinutes(1));
        var afterLock = account.SignIn(passwordOk: true, Now + UserAccount.LockoutDuration + TimeSpan.FromSeconds(1));

        // Assert
        fifth.ShouldBe(SignInResult.Locked);
        rightButLocked.ShouldBe(SignInResult.Locked);
        afterLock.ShouldBe(SignInResult.Ok);
    }

    [TestMethod]
    public void ResetPassword_RightToken_ChangesThePasswordAndConfirmsTheEmail()
    {
        // Arrange
        var account = NewAccount();
        account.RequestReset("reset-token", Now);

        // Act
        var done = account.ResetPassword("reset-token", "new-hash", Now.AddMinutes(10));

        // Assert
        done.ShouldBeTrue();
        account.PasswordHash.ShouldBe("new-hash");
        account.IsConfirmed.ShouldBeTrue();
        account.ResetPassword("reset-token", "other", Now.AddMinutes(11)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task RegisterHandler_NewAddress_StoresAndMailsTheConfirmationLink()
    {
        // Arrange
        var accounts = AccountsWith();
        var mail = new Mock<IEmailSender>();

        // Act
        await NewRegister(accounts, mail).Handle(new RegisterCommand("ann@example.org", "Ann", "Peak", "long-password-1"), CancellationToken.None);

        // Assert
        accounts.Verify(repo => repo.AddAsync(It.Is<UserAccount>(account => account.Email == "ann@example.org"), It.IsAny<CancellationToken>()), Times.Once);
        mail.Verify(sender => sender.SendAsync(
            It.Is<EmailMessage>(message => message.To == "ann@example.org" && message.Body.Contains("https://client.test/account/confirm?")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RegisterHandler_AlreadyConfirmed_MailsAHintAndChangesNothing()
    {
        // Arrange
        var existing = ConfirmedAccount();
        var accounts = AccountsWith(existing);
        var mail = new Mock<IEmailSender>();

        // Act
        await NewRegister(accounts, mail).Handle(new RegisterCommand(existing.Email, "Eve", "Other", "long-password-2"), CancellationToken.None);

        // Assert
        existing.FirstName.ShouldBe("Ann");
        accounts.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        mail.Verify(sender => sender.SendAsync(It.Is<EmailMessage>(message => message.Subject.Contains("already have")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ConfirmHandler_WrongToken_Throws()
    {
        // Arrange
        var account = NewAccount();

        // Act + Assert
        await Should.ThrowAsync<DomainException>(async () =>
            await new ConfirmEmailHandler(AccountsWith(account).Object, FixedTime()).Handle(new ConfirmEmailCommand(account.Id, "wrong"), CancellationToken.None));
    }

    [TestMethod]
    public async Task SignInHandler_UnknownEmail_SameAnswerAsWrongPassword()
    {
        // Act
        var exception = await Should.ThrowAsync<SignInFailedException>(async () =>
            await NewSignIn(AccountsWith(), passwordRight: true).Handle(new SignInCommand("nobody@example.org", "whatever-123"), CancellationToken.None));

        // Assert
        exception.Message.ShouldBe(SignInHandler.WrongCredentials);
    }

    [TestMethod]
    public async Task SignInHandler_RightPassword_ReturnsATokenAndSavesTheAttempt()
    {
        // Arrange
        var accounts = AccountsWith(ConfirmedAccount());

        // Act
        var result = await NewSignIn(accounts, passwordRight: true).Handle(new SignInCommand("ANN@example.org", "right-password"), CancellationToken.None);

        // Assert
        result.AccessToken.ShouldBe("token");
        result.Profile.DisplayName.ShouldBe("Ann Peak");
        accounts.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SignInHandler_NotConfirmed_ThrowsNotConfirmed()
    {
        // Act
        var exception = await Should.ThrowAsync<SignInFailedException>(async () =>
            await NewSignIn(AccountsWith(NewAccount()), passwordRight: true).Handle(new SignInCommand("ann@example.org", "right"), CancellationToken.None));

        // Assert
        exception.Reason.ShouldBe(SignInResult.NotConfirmed);
    }

    private static RegisterHandler NewRegister(Mock<IUserAccountRepository> accounts, Mock<IEmailSender> mail) =>
        new(accounts.Object, Hasher(true).Object, Tokens().Object, mail.Object, Links().Object, FixedTime());

    private static SignInHandler NewSignIn(Mock<IUserAccountRepository> accounts, bool passwordRight)
    {
        var issuer = new Mock<IAccessTokenIssuer>();
        issuer.Setup(tokens => tokens.Issue(It.IsAny<UserAccount>())).Returns(new AccessToken("token", Now.AddHours(12)));
        return new SignInHandler(accounts.Object, Hasher(passwordRight).Object, issuer.Object, FixedTime());
    }
}

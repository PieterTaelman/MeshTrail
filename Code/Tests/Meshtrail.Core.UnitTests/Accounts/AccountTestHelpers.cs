using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Accounts;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Meshtrail.Core.UnitTests.Accounts;

internal static class AccountTestHelpers
{
    public const string Token = "confirmation-token";

    public static readonly DateTimeOffset Now = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    public static FakeTimeProvider FixedTime() => new(Now);

    public static UserAccount NewAccount(string email = "ann@example.org") => UserAccount.Register(email, "Ann", "Peak", "hash", Token, Now);

    public static UserAccount ConfirmedAccount()
    {
        var account = NewAccount();
        account.ConfirmEmail(Token, Now);
        return account;
    }

    public static Mock<IUserAccountRepository> AccountsWith(params UserAccount[] known)
    {
        var accounts = new Mock<IUserAccountRepository>();
        accounts.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => known.FirstOrDefault(account => account.Id == id));
        accounts.Setup(repo => repo.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string email, CancellationToken _) => known.FirstOrDefault(account => account.Email == email));
        return accounts;
    }

    public static Mock<IPasswordHasher> Hasher(bool passwordRight)
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(passwordRight ? PasswordCheck.Right : PasswordCheck.Wrong);
        return hasher;
    }

    public static Mock<ISecureTokenGenerator> Tokens()
    {
        var tokens = new Mock<ISecureTokenGenerator>();
        tokens.Setup(generator => generator.NewToken()).Returns(Token);
        return tokens;
    }

    public static Mock<IClientLinks> Links()
    {
        var links = new Mock<IClientLinks>();
        links.Setup(l => l.ConfirmEmail(It.IsAny<Guid>(), It.IsAny<string>())).Returns("https://client.test/account/confirm?user=1&token=t");
        links.Setup(l => l.SignIn()).Returns("https://client.test/account/sign-in");
        return links;
    }
}

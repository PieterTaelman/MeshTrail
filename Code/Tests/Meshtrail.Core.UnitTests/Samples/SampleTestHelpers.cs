using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Samples;
using Moq;

namespace Meshtrail.Core.UnitTests.Samples;

/// <summary>Shared test data and mocks for the Samples tests. Keeps the test files focused on Arrange/Act/Assert.</summary>
internal static class SampleTestHelpers
{
    public const string UserName = "test-user";

    public static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    public static readonly byte[] StoredRowVersion = [0, 0, 0, 0, 0, 0, 0, 1];

    public static Sample ExistingSample(string name = "Existing") => Sample.Rehydrate(
        Guid.NewGuid(), name, "Existing description", Now.AddDays(-1), "creator", null, null, StoredRowVersion);

    public static Mock<ICurrentUser> CurrentUser()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Name).Returns(UserName);
        return currentUser;
    }

    public static TimeProvider FixedTime() => new FixedTimeProvider(Now);

    public static Mock<ISampleRepository> RepositoryReturning(Sample? sample)
    {
        var repository = new Mock<ISampleRepository>();
        repository.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(sample);
        return repository;
    }

    public static Mock<IPublisher> Publisher() => new();

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

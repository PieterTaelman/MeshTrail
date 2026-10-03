using FluentValidation;
using Meshtrail.Core.Application.Behaviors;
using Shouldly;
using static Meshtrail.Core.UnitTests.Behaviors.ValidationBehaviorTestHelpers;

namespace Meshtrail.Core.UnitTests.Behaviors;

[TestClass]
public sealed class ValidationBehaviorTests
{
    [TestMethod]
    public async Task Handle_ValidMessage_CallsNext()
    {
        // Arrange
        var behavior = new ValidationBehavior<TestCommand, string>([new TestCommandValidator()]);

        // Act
        var result = await behavior.Handle(new TestCommand("ok", "ok"), NextReturning("handled"), CancellationToken.None);

        // Assert
        result.ShouldBe("handled");
    }

    [TestMethod]
    public async Task Handle_InvalidMessage_ThrowsWithAllFailuresAndSkipsNext()
    {
        // Arrange
        var behavior = new ValidationBehavior<TestCommand, string>([new TestCommandValidator()]);
        var nextCalled = false;

        // Act
        var exception = await Should.ThrowAsync<ValidationException>(async () =>
            await behavior.Handle(new TestCommand(string.Empty, string.Empty), NextTracking(() => nextCalled = true), CancellationToken.None));

        // Assert
        exception.Errors.Select(error => error.PropertyName).ShouldBe(["First", "Second"], ignoreOrder: true);
        nextCalled.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Handle_NoValidators_CallsNext()
    {
        // Arrange
        var behavior = new ValidationBehavior<TestCommand, string>([]);

        // Act
        var result = await behavior.Handle(new TestCommand(string.Empty, string.Empty), NextReturning("handled"), CancellationToken.None);

        // Assert
        result.ShouldBe("handled");
    }
}

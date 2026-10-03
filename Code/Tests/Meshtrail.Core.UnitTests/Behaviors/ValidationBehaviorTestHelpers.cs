using FluentValidation;
using Mediator;

namespace Meshtrail.Core.UnitTests.Behaviors;

/// <summary>Small fake message, validator and "next" delegates so the behavior can be tested without a real handler.</summary>
internal static class ValidationBehaviorTestHelpers
{
    public sealed record TestCommand(string First, string Second) : ICommand<string>;

    public sealed class TestCommandValidator : AbstractValidator<TestCommand>
    {
        public TestCommandValidator()
        {
            RuleFor(command => command.First).NotEmpty();
            RuleFor(command => command.Second).NotEmpty();
        }
    }

    public static MessageHandlerDelegate<TestCommand, string> NextReturning(string response) =>
        (_, _) => ValueTask.FromResult(response);

    public static MessageHandlerDelegate<TestCommand, string> NextTracking(Action onCalled) =>
        (_, _) =>
        {
            onCalled();
            return ValueTask.FromResult("handled");
        };
}

using FluentValidation;

namespace Meshtrail.Core.Application.UseCases.Samples.Commands.DeleteSample;

public sealed class DeleteSampleValidator : AbstractValidator<DeleteSampleCommand>
{
    public DeleteSampleValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}

using FluentValidation;
using Meshtrail.Core.Domain.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples.Commands.CreateSample;

/// <summary>Shape checks only (required, length). Business rules belong in <see cref="Sample"/>.</summary>
public sealed class CreateSampleValidator : AbstractValidator<CreateSampleCommand>
{
    public CreateSampleValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Sample.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(Sample.DescriptionMaxLength);
    }
}

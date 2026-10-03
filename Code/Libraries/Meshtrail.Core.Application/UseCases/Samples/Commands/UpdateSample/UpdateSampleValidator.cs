using FluentValidation;
using Meshtrail.Core.Domain.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples.Commands.UpdateSample;

public sealed class UpdateSampleValidator : AbstractValidator<UpdateSampleCommand>
{
    public UpdateSampleValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Sample.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(Sample.DescriptionMaxLength);
        RuleFor(command => command.RowVersion)
            .NotEmpty()
            .Must(BeBase64).WithMessage("'Row Version' must be the base64 value returned when the sample was loaded.");
    }

    private static bool BeBase64(string value) =>
        Convert.TryFromBase64String(value, new byte[value.Length], out _);
}

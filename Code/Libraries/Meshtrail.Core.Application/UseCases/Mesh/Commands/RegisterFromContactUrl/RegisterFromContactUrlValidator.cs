using FluentValidation;
using Meshtrail.Core.Application.Abstractions;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RegisterFromContactUrl;

public sealed class RegisterFromContactUrlValidator : AbstractValidator<RegisterFromContactUrlCommand>
{
    /// <summary>Real links are ~120 characters; anything much longer is not one.</summary>
    public const int UrlMaxLength = 1000;

    public RegisterFromContactUrlValidator(IContactUrlParser parser)
    {
        RuleFor(command => command.Url)
            .NotEmpty()
            .MaximumLength(UrlMaxLength)
            .Custom((url, context) =>
            {
                if (!string.IsNullOrWhiteSpace(url) && !parser.TryParse(url, out _, out var error))
                {
                    context.AddFailure(nameof(RegisterFromContactUrlCommand.Url), error);
                }
            });
    }
}

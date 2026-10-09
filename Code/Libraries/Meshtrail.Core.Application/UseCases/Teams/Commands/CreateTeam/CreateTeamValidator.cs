using FluentValidation;
using Meshtrail.Core.Domain.Teams;

namespace Meshtrail.Core.Application.UseCases.Teams.Commands.CreateTeam;

public sealed class CreateTeamValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Team.NameMaxLength);
        RuleFor(command => command.ChannelName).NotEmpty().MaximumLength(Team.ChannelNameMaxLength);
    }
}

using FluentValidation;
using Meshtrail.Core.Domain.Teams;

namespace Meshtrail.Core.Application.UseCases.Teams.Commands.JoinTeam;

public sealed class JoinTeamValidator : AbstractValidator<JoinTeamCommand>
{
    public JoinTeamValidator()
    {
        RuleFor(command => command.Code).NotEmpty().MaximumLength(Team.JoinCodeLength * 2);
    }
}

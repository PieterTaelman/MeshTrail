using FluentValidation;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Queries.AuthenticateGateway;

public sealed class AuthenticateGatewayValidator : AbstractValidator<AuthenticateGatewayQuery>
{
    public AuthenticateGatewayValidator()
    {
        RuleFor(query => query.UserName).MaximumLength(MeshGateway.MqttUserNameMaxLength);
        RuleFor(query => query.Password).MaximumLength(200);
    }
}

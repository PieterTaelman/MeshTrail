using Mediator;
using Meshtrail.Core.Application.Repositories;

namespace Meshtrail.Core.Application.UseCases.Gateways.Queries.AuthenticateGateway;

public sealed class AuthenticateGatewayHandler(IMeshGatewayRepository gateways) : IQueryHandler<AuthenticateGatewayQuery, bool>
{
    public async ValueTask<bool> Handle(AuthenticateGatewayQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(query.UserName) || string.IsNullOrEmpty(query.Password))
        {
            return false;
        }

        var gateway = await gateways.GetByMqttUserNameAsync(query.UserName, cancellationToken);
        return gateway?.PasswordMatches(query.Password) == true;
    }
}

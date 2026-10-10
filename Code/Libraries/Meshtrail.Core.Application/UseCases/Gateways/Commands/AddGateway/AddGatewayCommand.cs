using Mediator;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.AddGateway;

/// <summary>
/// Makes node <paramref name="NodeNum"/> a gateway of the current user: MQTT credentials that only work for that node.
/// The password is returned this one time.
/// </summary>
public sealed record AddGatewayCommand(uint NodeNum) : ICommand<GatewayCredentialsDto>;
